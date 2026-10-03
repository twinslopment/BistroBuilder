using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicOperationalStageMetric
    {
        internal string StageId { get; set; } = string.Empty;
        internal int Samples { get; set; }
        internal int Failures { get; set; }
        internal long AverageMilliseconds { get; set; }
        internal long P95Milliseconds { get; set; }
        internal long MaximumMilliseconds { get; set; }
    }

    internal sealed class SavicOperationalMetrics
    {
        internal int EligibleJobs { get; set; }
        internal int ActiveJobs { get; set; }
        internal int TerminalJobs { get; set; }
        internal int DoneJobs { get; set; }
        internal int NeedsReviewJobs { get; set; }
        internal int FailedJobs { get; set; }
        internal int CancelledJobs { get; set; }
        internal double SuccessRatePercent { get; set; }
        internal long AverageDurationMilliseconds { get; set; }
        internal long P95DurationMilliseconds { get; set; }
        internal long SlowestJobMilliseconds { get; set; }
        internal string SlowestJobName { get; set; } = string.Empty;
        internal string TopIssueReason { get; set; } = string.Empty;
        internal int TopIssueReasonCount { get; set; }
        internal IReadOnlyList<SavicOperationalStageMetric> Stages { get; set; } =
            Array.Empty<SavicOperationalStageMetric>();
    }

    internal static class SavicOperationalAnalytics
    {
        internal const string Version = "1.0.0";

        internal static SavicOperationalMetrics Build(
            IEnumerable<SavicJobRecord> jobs)
        {
            List<SavicJobRecord> all =
                jobs?
                    .Where(job => job != null)
                    .ToList() ??
                new List<SavicJobRecord>();

            List<SavicJobRecord> eligible =
                all
                    .Where(job => job.batchEligible)
                    .ToList();

            List<SavicJobRecord> terminal =
                eligible
                    .Where(
                        job =>
                            IsTerminal(
                                job.state))
                    .ToList();

            List<long> durations =
                terminal
                    .Where(job => job.lastDurationMilliseconds >= 0)
                    .Select(
                        job =>
                            Math.Max(
                                0L,
                                job.lastDurationMilliseconds))
                    .OrderBy(value => value)
                    .ToList();

            SavicJobRecord slowest =
                terminal
                    .OrderByDescending(
                        job =>
                            job.lastDurationMilliseconds)
                    .FirstOrDefault();

            var reasonGroup =
                terminal
                    .Where(
                        job =>
                            !string.IsNullOrWhiteSpace(
                                job.reasonCode) &&
                            !string.Equals(
                                job.reasonCode,
                                "PUBLISHED",
                                StringComparison.OrdinalIgnoreCase))
                    .GroupBy(
                        job =>
                            job.reasonCode,
                        StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(
                        group =>
                            group.Count())
                    .ThenBy(
                        group =>
                            group.Key,
                        StringComparer.Ordinal)
                    .FirstOrDefault();

            List<SavicOperationalStageMetric> stages =
                eligible
                    .SelectMany(
                        job =>
                            job.stageTimings ??
                            new List<SavicProcessingStageRecord>())
                    .Where(
                        stage =>
                            stage != null &&
                            !string.IsNullOrWhiteSpace(
                                stage.stageId))
                    .GroupBy(
                        stage =>
                            stage.stageId,
                        StringComparer.Ordinal)
                    .Select(
                        group =>
                        {
                            List<long> stageDurations =
                                group
                                    .Select(
                                        stage =>
                                            Math.Max(
                                                0L,
                                                stage.durationMilliseconds))
                                    .OrderBy(
                                        value =>
                                            value)
                                    .ToList();

                            return new SavicOperationalStageMetric
                            {
                                StageId = group.Key,
                                Samples = stageDurations.Count,
                                Failures =
                                    group.Count(
                                        stage =>
                                            string.Equals(
                                                stage.result,
                                                "FAIL",
                                                StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(
                                                stage.result,
                                                "EXCEPTION",
                                                StringComparison.OrdinalIgnoreCase)),
                                AverageMilliseconds =
                                    Average(
                                        stageDurations),
                                P95Milliseconds =
                                    Percentile95(
                                        stageDurations),
                                MaximumMilliseconds =
                                    stageDurations.Count == 0
                                        ? 0
                                        : stageDurations[
                                            stageDurations.Count - 1]
                            };
                        })
                    .OrderByDescending(
                        metric =>
                            metric.P95Milliseconds)
                    .ThenBy(
                        metric =>
                            metric.StageId,
                        StringComparer.Ordinal)
                    .ToList();

            int done =
                terminal.Count(
                    job =>
                        IsState(
                            job.state,
                            SavicJobState.Done));

            return new SavicOperationalMetrics
            {
                EligibleJobs =
                    eligible.Count,
                ActiveJobs =
                    eligible.Count(
                        job =>
                            IsState(
                                job.state,
                                SavicJobState.Ingested) ||
                            IsState(
                                job.state,
                                SavicJobState.Processing)),
                TerminalJobs =
                    terminal.Count,
                DoneJobs =
                    done,
                NeedsReviewJobs =
                    terminal.Count(
                        job =>
                            IsState(
                                job.state,
                                SavicJobState.NeedsReview)),
                FailedJobs =
                    terminal.Count(
                        job =>
                            IsState(
                                job.state,
                                SavicJobState.FailedProcessing) ||
                            IsState(
                                job.state,
                                SavicJobState.FailedSource) ||
                            IsState(
                                job.state,
                                SavicJobState.Quarantined)),
                CancelledJobs =
                    terminal.Count(
                        job =>
                            IsState(
                                job.state,
                                SavicJobState.Cancelled)),
                SuccessRatePercent =
                    terminal.Count == 0
                        ? 0d
                        : done * 100d /
                          terminal.Count,
                AverageDurationMilliseconds =
                    Average(
                        durations),
                P95DurationMilliseconds =
                    Percentile95(
                        durations),
                SlowestJobMilliseconds =
                    slowest?.lastDurationMilliseconds ?? 0L,
                SlowestJobName =
                    slowest?.originalFileName ?? string.Empty,
                TopIssueReason =
                    reasonGroup?.Key ??
                    string.Empty,
                TopIssueReasonCount =
                    reasonGroup?.Count() ?? 0,
                Stages =
                    stages
            };
        }

        internal static long Percentile95(
            IReadOnlyList<long> sortedValues)
        {
            if (sortedValues == null ||
                sortedValues.Count == 0)
            {
                return 0L;
            }

            int index =
                Math.Max(
                    0,
                    Math.Min(
                        sortedValues.Count - 1,
                        (int)Math.Ceiling(
                            sortedValues.Count *
                            0.95d) -
                        1));

            return sortedValues[index];
        }

        private static long Average(
            IReadOnlyList<long> values)
        {
            if (values == null ||
                values.Count == 0)
            {
                return 0L;
            }

            double average =
                values.Average(
                    value =>
                        (double)value);

            return (long)Math.Round(
                average,
                MidpointRounding.AwayFromZero);
        }

        private static bool IsTerminal(
            string state)
        {
            return IsState(state, SavicJobState.Done) ||
                   IsState(state, SavicJobState.NeedsReview) ||
                   IsState(state, SavicJobState.FailedProcessing) ||
                   IsState(state, SavicJobState.FailedSource) ||
                   IsState(state, SavicJobState.Quarantined) ||
                   IsState(state, SavicJobState.Cancelled);
        }

        private static bool IsState(
            string state,
            SavicJobState expected)
        {
            return string.Equals(
                state,
                expected.ToString(),
                StringComparison.Ordinal);
        }
    }
}
