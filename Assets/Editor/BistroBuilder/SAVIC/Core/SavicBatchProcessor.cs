using System;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicBatchProcessor
    {
        internal const string Version = "1.0.0";

        // One asset operation is deliberately atomic. Time-slicing happens
        // between jobs so an interrupted asset never remains half-published.
        private const long SlowJobWarningMilliseconds = 2000;

        private readonly SavicJobStore jobs;
        private readonly SavicSourceProcessingService processing;
        private bool executing;

        internal SavicBatchProcessor(
            SavicJobStore jobs,
            SavicSourceProcessingService processing)
        {
            this.jobs =
                jobs ?? throw new ArgumentNullException(nameof(jobs));

            this.processing =
                processing ??
                throw new ArgumentNullException(nameof(processing));
        }

        internal bool IsExecuting => executing;

        internal void RecoverAfterDomainReload()
        {
            int recovered =
                jobs.RecoverInterruptedJobs();

            if (recovered > 0)
            {
                Debug.LogWarning(
                    "[SAVIC] Batch recovery re-queued " +
                    recovered +
                    " interrupted job(s) after reload.");
            }
        }

        internal bool TickOne()
        {
            if (executing ||
                jobs.IsPaused)
            {
                return false;
            }

            if (!jobs.TryClaimNextProcessable(
                    out SavicJobRecord job))
            {
                return false;
            }

            executing = true;
            Stopwatch stopwatch =
                Stopwatch.StartNew();

            SavicSourceProcessingOutcome outcome;

            try
            {
                outcome =
                    processing.ProcessBySavicId(
                        job.manifestSavicId);
            }
            catch (Exception exception)
            {
                outcome =
                    new SavicSourceProcessingOutcome(
                        false,
                        "FAILED_PROCESSING",
                        "Unhandled batch processing failure: " +
                        exception.Message,
                        null);

                Debug.LogError(
                    "[SAVIC] Batch job failed safely: " +
                    exception);
            }
            finally
            {
                stopwatch.Stop();
                executing = false;
            }

            jobs.CompleteProcessing(
                job.jobId,
                outcome,
                stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds >=
                SlowJobWarningMilliseconds)
            {
                Debug.LogWarning(
                    "[SAVIC] Slow batch asset operation: " +
                    job.originalFileName +
                    " took " +
                    stopwatch.ElapsedMilliseconds +
                    " ms. Queue execution remains serialized.");
            }

            return true;
        }
    }
}
