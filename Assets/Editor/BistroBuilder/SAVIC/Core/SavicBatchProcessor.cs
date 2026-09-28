using System;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicBatchProcessor
    {
        internal const string Version = "1.2.0";

        // Individual mutation stages remain atomic. High-confidence generic
        // assets may yield after source preparation so import and publication
        // do not monopolize one Editor tick.
        private const long TargetTickBudgetMilliseconds = 16;
        internal const long SlowJobWarningMilliseconds = 2000;
        private const double MaximumCooldownSeconds = 0.25;

        private readonly SavicJobStore jobs;
        private readonly SavicSourceProcessingService processing;
        private bool executing;
        private double nextEligibleEditorTime;
        private long lastOperationMilliseconds;
        private int budgetOverrunCount;

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
        internal long LastOperationMilliseconds => lastOperationMilliseconds;
        internal int BudgetOverrunCount => budgetOverrunCount;
        internal bool IsCoolingDown =>
            EditorApplication.timeSinceStartup <
            nextEligibleEditorTime;

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
            return TickOneCore(false);
        }

        internal bool TickOneIgnoringCooldownForDiagnostics()
        {
            return TickOneCore(true);
        }

        private bool TickOneCore(
            bool ignoreCooldown)
        {
            if (executing ||
                jobs.IsPaused ||
                (!ignoreCooldown &&
                 EditorApplication.timeSinceStartup <
                 nextEligibleEditorTime))
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
            bool yieldedAfterPreparation =
                false;
            SavicSourcePreparationStage yieldedStage =
                SavicSourcePreparationStage.None;

            try
            {
                SavicSourcePreparationStage preparationStage =
                    ParsePreparationStage(
                        job);

                if (preparationStage ==
                    SavicSourcePreparationStage.None &&
                    processing.TryRouteBeforeImportBySavicId(
                        job.manifestSavicId,
                        out SavicSourceProcessingOutcome routedOutcome))
                {
                    outcome =
                        routedOutcome;
                }
                else if (preparationStage ==
                         SavicSourcePreparationStage.None)
                {
                    outcome =
                        processing.MaterializeSourceMirrorBySavicId(
                            job.manifestSavicId);

                    yieldedAfterPreparation =
                        outcome.Succeeded &&
                        string.Equals(
                            outcome.Status,
                            "SOURCE_MATERIALIZED",
                            StringComparison.Ordinal);

                    yieldedStage =
                        SavicSourcePreparationStage
                            .MirrorMaterialized;
                }
                else if (preparationStage ==
                         SavicSourcePreparationStage
                             .MirrorMaterialized)
                {
                    outcome =
                        processing.PrepareSourceImportBySavicId(
                            job.manifestSavicId);

                    yieldedAfterPreparation =
                        outcome.Succeeded &&
                        string.Equals(
                            outcome.Status,
                            "SOURCE_PREPARED",
                            StringComparison.Ordinal);

                    yieldedStage =
                        SavicSourcePreparationStage
                            .SourceImported;
                }
                else
                {
                    outcome =
                        processing.ProcessBySavicId(
                            job.manifestSavicId);
                }
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

            lastOperationMilliseconds =
                stopwatch.ElapsedMilliseconds;

            if (yieldedAfterPreparation)
            {
                jobs.YieldPreparationStage(
                    job.jobId,
                    outcome,
                    yieldedStage,
                    lastOperationMilliseconds);
            }
            else
            {
                jobs.CompleteProcessing(
                    job.jobId,
                    outcome,
                    lastOperationMilliseconds);
            }

            double cooldownSeconds =
                ComputeCooldownSeconds(
                    lastOperationMilliseconds);

            if (cooldownSeconds > 0d)
            {
                budgetOverrunCount++;
                nextEligibleEditorTime =
                    EditorApplication.timeSinceStartup +
                    cooldownSeconds;
            }
            else
            {
                nextEligibleEditorTime = 0d;
            }

            if (lastOperationMilliseconds >=
                SlowJobWarningMilliseconds)
            {
                Debug.LogWarning(
                    "[SAVIC] Slow batch asset operation: " +
                    job.originalFileName +
                    " took " +
                    lastOperationMilliseconds +
                    " ms. Atomic stage execution remains serialized.");
            }

            return true;
        }

        private static SavicSourcePreparationStage
            ParsePreparationStage(
                SavicJobRecord job)
        {
            if (job == null)
                return SavicSourcePreparationStage.None;

            if (Enum.TryParse(
                    job.preparationStage,
                    true,
                    out SavicSourcePreparationStage stage))
            {
                return stage;
            }

            return job.sourcePrepared
                ? SavicSourcePreparationStage.SourceImported
                : SavicSourcePreparationStage.None;
        }

        internal static double ComputeCooldownSeconds(
            long operationMilliseconds)
        {
            if (operationMilliseconds <=
                TargetTickBudgetMilliseconds)
            {
                return 0d;
            }

            double overrunMilliseconds =
                operationMilliseconds -
                TargetTickBudgetMilliseconds;

            double cooldown =
                Math.Max(
                    0.016d,
                    overrunMilliseconds /
                    1000d *
                    0.15d);

            return Math.Min(
                MaximumCooldownSeconds,
                cooldown);
        }
    }
}
