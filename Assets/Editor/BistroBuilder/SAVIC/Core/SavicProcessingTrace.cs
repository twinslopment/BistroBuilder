using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicProcessingTrace
    {
        internal const string Version = "1.0.0";

        private readonly Stopwatch total =
            Stopwatch.StartNew();

        private readonly List<SavicProcessingStageRecord> stages =
            new List<SavicProcessingStageRecord>();

        internal string LastStageId =>
            stages.Count == 0
                ? string.Empty
                : stages[stages.Count - 1].stageId;

        internal T Measure<T>(
            string stageId,
            Func<T> operation,
            Func<T, bool> succeeded = null,
            Func<T, string> detail = null)
        {
            if (string.IsNullOrWhiteSpace(stageId))
                throw new ArgumentException("Stage id is required.", nameof(stageId));

            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            try
            {
                T result =
                    operation();

                stopwatch.Stop();

                bool passed =
                    succeeded == null ||
                    succeeded(result);

                Record(
                    stageId,
                    passed ? "PASS" : "FAIL",
                    stopwatch.ElapsedMilliseconds,
                    detail?.Invoke(result) ?? string.Empty);

                return result;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();

                Record(
                    stageId,
                    "EXCEPTION",
                    stopwatch.ElapsedMilliseconds,
                    exception.Message);

                throw;
            }
        }

        internal void RecordReuse(
            string stageId,
            string detail)
        {
            Record(
                stageId,
                "REUSED",
                0,
                detail);
        }

        internal SavicProcessingDiagnostics Finish(
            string reasonCode,
            string primaryStage,
            string summary)
        {
            total.Stop();

            return new SavicProcessingDiagnostics
            {
                traceVersion = Version,
                reasonCode = reasonCode ?? string.Empty,
                primaryStage =
                    string.IsNullOrWhiteSpace(primaryStage)
                        ? LastStageId
                        : primaryStage,
                summary = summary ?? string.Empty,
                totalMilliseconds = total.ElapsedMilliseconds,
                completedUtc = DateTime.UtcNow.ToString("O"),
                stages = stages
                    .Select(
                        stage =>
                            new SavicProcessingStageRecord
                            {
                                stageId = stage.stageId,
                                result = stage.result,
                                durationMilliseconds =
                                    stage.durationMilliseconds,
                                detail = stage.detail
                            })
                    .ToList()
            };
        }

        private void Record(
            string stageId,
            string result,
            long durationMilliseconds,
            string detail)
        {
            stages.Add(
                new SavicProcessingStageRecord
                {
                    stageId = stageId ?? string.Empty,
                    result = result ?? string.Empty,
                    durationMilliseconds =
                        Math.Max(0L, durationMilliseconds),
                    detail = detail ?? string.Empty
                });
        }
    }
}
