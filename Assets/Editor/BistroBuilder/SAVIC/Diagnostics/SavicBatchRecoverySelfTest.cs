using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBatchRecoverySelfTest
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Batch Recovery Self-Test",
            false,
            128)]
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
            string sandboxRoot =
                Path.Combine(
                    Path.GetTempPath(),
                    "BistroBuilder_SAVIC_Batch_" +
                    Guid.NewGuid().ToString("N"));

            try
            {
                SavicStorageLayout layout =
                    new SavicStorageLayout(sandboxRoot);

                layout.EnsureInfrastructure();

                ValidatePauseCancelAndRecovery(layout);
                long stressMilliseconds =
                    ValidateStressPersistence(layout);

                Debug.Log(
                    "[SAVIC] BATCH RECOVERY SELF-TEST - PASS\n" +
                    "Pause/resume: PASS\n" +
                    "Cancellation: PASS\n" +
                    "Interrupted job recovery: PASS\n" +
                    "Deterministic claim order: PASS\n" +
                    "Stress snapshots: 100 / 500 / 2000 PASS\n" +
                    "2000-job persistence+reload: " +
                    stressMilliseconds +
                    " ms");
            }
            finally
            {
                if (Directory.Exists(sandboxRoot))
                {
                    try
                    {
                        Directory.Delete(
                            sandboxRoot,
                            true);
                    }
                    catch
                    {
                        // Diagnostics must not mask the actual result
                        // because a temp directory is briefly locked.
                    }
                }
            }
        }

        private static void ValidatePauseCancelAndRecovery(
            SavicStorageLayout layout)
        {
            SavicJobStore store =
                new SavicJobStore(layout);

            SavicManifest first =
                CreateManifest(
                    "manifest-a",
                    new string('a', 64),
                    "a.glb");

            SavicManifest second =
                CreateManifest(
                    "manifest-b",
                    new string('b', 64),
                    "b.glb");

            SavicJobRecord firstJob =
                store.RecordIngested(
                    first,
                    false,
                    "fixture");

            SavicJobRecord secondJob =
                store.RecordIngested(
                    second,
                    false,
                    "fixture");

            Require(
                store.PendingProcessCount == 2,
                "Expected two processable jobs.");

            store.SetPaused(true);

            Require(
                store.IsPaused,
                "Pause state was not persisted in memory.");

            Require(
                !store.TryClaimNextProcessable(out _),
                "Paused queue claimed a job.");

            SavicJobStore pausedReload =
                new SavicJobStore(layout);

            Require(
                pausedReload.IsPaused,
                "Pause state did not survive reload.");

            pausedReload.SetPaused(false);

            Require(
                pausedReload.TryClaimNextProcessable(
                    out SavicJobRecord claimed),
                "Queue could not claim the first job.");

            Require(
                claimed.jobId == firstJob.jobId,
                "Queue claim order is not deterministic.");

            Require(
                string.Equals(
                    claimed.state,
                    SavicJobState.Processing.ToString(),
                    StringComparison.Ordinal),
                "Claimed job did not enter Processing.");

            SavicJobStore recovered =
                new SavicJobStore(layout);

            Require(
                recovered.RecoverInterruptedJobs() == 1,
                "Interrupted job was not recovered exactly once.");

            SavicJobRecord recoveredFirst =
                recovered.Jobs.First(
                    job => job.jobId == firstJob.jobId);

            Require(
                string.Equals(
                    recoveredFirst.state,
                    SavicJobState.Ingested.ToString(),
                    StringComparison.Ordinal),
                "Recovered job was not re-queued.");

            Require(
                recovered.RequestCancel(
                    firstJob.jobId),
                "Cancellation request failed.");

            SavicJobRecord cancelled =
                recovered.Jobs.First(
                    job => job.jobId == firstJob.jobId);

            Require(
                string.Equals(
                    cancelled.state,
                    SavicJobState.Cancelled.ToString(),
                    StringComparison.Ordinal),
                "Queued cancellation did not become terminal.");

            Require(
                recovered.TryClaimNextProcessable(
                    out SavicJobRecord next),
                "Second job was not claimable.");

            Require(
                next.jobId == secondJob.jobId,
                "Cancelled job disturbed deterministic ordering.");
        }

        private static long ValidateStressPersistence(
            SavicStorageLayout layout)
        {
            int[] sizes = { 100, 500, 2000 };
            long lastElapsed = 0;

            for (int pass = 0;
                 pass < sizes.Length;
                 pass++)
            {
                int size = sizes[pass];

                SavicQueueSnapshot snapshot =
                    new SavicQueueSnapshot
                    {
                        schemaVersion =
                            SavicVersion.QueueSchemaVersion,
                        savedUtc =
                            DateTime.UtcNow.ToString("O"),
                        jobs =
                            new List<SavicJobRecord>(
                                size)
                    };

                for (int index = 0;
                     index < size;
                     index++)
                {
                    snapshot.jobs.Add(
                        new SavicJobRecord
                        {
                            jobId =
                                "stress-" +
                                index.ToString("D5"),
                            state =
                                SavicJobState.Ingested
                                    .ToString(),
                            sourceHash =
                                SavicHashService
                                    .ComputeSha256Text(
                                        "stress-" +
                                        index),
                            originalFileName =
                                "fixture-" +
                                index +
                                ".glb",
                            manifestSavicId =
                                "manifest-" +
                                index,
                            createdUtc =
                                DateTime.UtcNow
                                    .ToString("O"),
                            updatedUtc =
                                DateTime.UtcNow
                                    .ToString("O"),
                            attempts = 1,
                            batchEligible = true,
                            checkpoint = "INGESTED"
                        });
                }

                Stopwatch stopwatch =
                    Stopwatch.StartNew();

                SavicAtomicFile.WriteJson(
                    layout.QueueSnapshotPath,
                    snapshot);

                SavicJobStore reloaded =
                    new SavicJobStore(layout);

                Require(
                    reloaded.Jobs.Count == size,
                    "Stress queue reload lost jobs at size " +
                    size +
                    ".");

                Require(
                    reloaded.PendingProcessCount == size,
                    "Stress pending count is incorrect at size " +
                    size +
                    ".");

                Require(
                    reloaded.TryClaimNextProcessable(
                        out SavicJobRecord claimed),
                    "Stress queue could not claim a job.");

                Require(
                    claimed.jobId ==
                    "stress-00000",
                    "Stress queue lost deterministic ordering.");

                stopwatch.Stop();
                lastElapsed =
                    stopwatch.ElapsedMilliseconds;

                // This is deliberately generous: the test guards
                // catastrophic stalls, not machine-specific microbenchmarks.
                Require(
                    lastElapsed < 10000,
                    "Queue persistence/reload exceeded 10 seconds at size " +
                    size +
                    ".");
            }

            return lastElapsed;
        }

        private static SavicManifest CreateManifest(
            string savicId,
            string sourceHash,
            string fileName)
        {
            return new SavicManifest
            {
                savicId = savicId,
                source =
                    new SavicSourceRecord
                    {
                        sourceHash = sourceHash,
                        originalFileName = fileName,
                        archivedRelativePath =
                            "ContentSource/" +
                            fileName,
                        sourceKind =
                            SavicSourceKind.Model3D
                                .ToString()
                    }
            };
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
