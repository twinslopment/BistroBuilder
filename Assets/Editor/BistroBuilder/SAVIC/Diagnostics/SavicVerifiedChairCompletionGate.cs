using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicVerifiedChairCompletionGate
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Verified Chair Completion Gate", false, 132)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Chair publication requires a graphics device; run without -nographics or in the Unity Editor.");

            SavicNormalizedChairCalibrationSelfTest.RunFromCommandLine();
            SavicEditorContext context = SavicEditorContext.Instance;
            if (context.Jobs.IsPaused)
                throw new InvalidOperationException(
                    "SAVIC queue is paused; the completion gate will not override it.");

            int queued = context.CanonicalReconciliation
                .RetryVerifiedChairReviewsForCurrentPlanner(8);
            int previewQueued = context.CanonicalReconciliation
                .RetryVerifiedChairPreviewFailures(8);
            string version = SavicChairAuthoringPlanner.Version;
            HashSet<string> targetIds = new HashSet<string>(
                context.Jobs.Jobs
                    .Where(job => job != null &&
                        (string.Equals(job.lastAutomaticChairPlannerRetryVersion,
                             version, StringComparison.Ordinal) ||
                         string.Equals(job.lastAutomaticChairPreviewRetryVersion,
                             SavicPreviewRenderer.Version, StringComparison.Ordinal)))
                    .Select(job => job.jobId),
                StringComparer.Ordinal);
            if (targetIds.Count == 0)
                throw new InvalidOperationException(
                    "No verified chair review was queued by the current planner revision.");

            for (int tick = 0; tick < 512; tick++)
            {
                SavicJobRecord[] targets = context.Jobs.Jobs
                    .Where(job => job != null && targetIds.Contains(job.jobId))
                    .ToArray();
                if (targets.All(job =>
                        !string.Equals(job.state,
                            SavicJobState.Ingested.ToString(), StringComparison.Ordinal) &&
                        !string.Equals(job.state,
                            SavicJobState.Processing.ToString(), StringComparison.Ordinal)))
                    break;

                if (!context.Batch.TickOneIgnoringCooldownForDiagnostics())
                    throw new InvalidOperationException(
                        "SAVIC could not advance a queued verified chair.");
            }

            SavicJobRecord[] finalTargets = context.Jobs.Jobs
                .Where(job => job != null && targetIds.Contains(job.jobId))
                .ToArray();
            string[] incomplete = finalTargets
                .Where(job => job.state != SavicJobState.Done.ToString() ||
                    job.reasonCode != "PUBLISHED")
                .Select(job => job.originalFileName + ": " + job.state +
                    "/" + job.reasonCode + " - " + job.message)
                .ToArray();
            if (incomplete.Length > 0)
                throw new InvalidOperationException(
                    "Verified chair completion failed: " +
                    string.Join("; ", incomplete));

            SavicTableSaveLoadStateProbe.RunChairFromCommandLine();
            Debug.Log("[SAVIC] VERIFIED CHAIR COMPLETION GATE - PASS: " +
                      "queued=" + (queued + previewQueued) + ", verified published=" +
                      finalTargets.Length + ", chair SaveLoad state=PASS.");
        }
    }
}
