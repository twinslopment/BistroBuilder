using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicAutonomousClassificationReport
    {
        public string generatedUtc = string.Empty;
        public string classifierVersion = string.Empty;
        public int refreshed;
        public int queued;
        public int skipped;
        public int reviews;
        public int recognized;
        public int unknown;
        public List<SavicAutonomousClassificationRow> rows =
            new List<SavicAutonomousClassificationRow>();
    }

    [Serializable]
    internal sealed class SavicAutonomousClassificationRow
    {
        public string fileName = string.Empty;
        public string savicId = string.Empty;
        public string jobState = string.Empty;
        public string reasonCode = string.Empty;
        public string type = string.Empty;
        public string confidence = string.Empty;
        public bool hasRegisteredFamily;
        public string evidence = string.Empty;
    }

    public static class SavicAutonomousClassificationAudit
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Refresh Reviewed Classifications", false, 132)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            SavicEditorContext context = SavicEditorContext.Instance;
            SavicClassificationRefreshResult refresh = context.CanonicalReconciliation
                .RefreshReviewedClassifications(64,
                    context.SourceProcessing.HasRegisteredFamily);
            SavicAutonomousClassificationReport report =
                new SavicAutonomousClassificationReport
                {
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    classifierVersion = SavicContentClassifier.Version,
                    refreshed = refresh.Updated,
                    queued = refresh.Queued,
                    skipped = refresh.Skipped
                };

            foreach (SavicJobRecord job in context.Jobs.Jobs
                         .Where(candidate => candidate != null &&
                             candidate.batchEligible &&
                             candidate.state == SavicJobState.NeedsReview.ToString())
                         .OrderBy(candidate => candidate.originalFileName,
                             StringComparer.OrdinalIgnoreCase))
            {
                if (!context.Manifests.TryGetBySavicId(job.manifestSavicId,
                        out SavicManifest manifest))
                    continue;

                SavicClassificationRecord classification = manifest.classification;
                string type = classification?.type ?? "Unknown";
                report.rows.Add(new SavicAutonomousClassificationRow
                {
                    fileName = job.originalFileName,
                    savicId = job.manifestSavicId,
                    jobState = job.state,
                    reasonCode = job.reasonCode,
                    type = type,
                    confidence = classification?.confidence ?? "UNKNOWN",
                    hasRegisteredFamily = context.SourceProcessing.HasRegisteredFamily(type),
                    evidence = classification?.evidence ?? string.Empty
                });
                report.reviews++;
                if (type == "Unknown") report.unknown++;
                else report.recognized++;
            }

            string path = Path.Combine(context.Layout.LogsRoot,
                "autonomous-classification.json");
            SavicAtomicFile.WriteJson(path, report);
            Debug.Log("[SAVIC] Autonomous classification: refreshed=" +
                      report.refreshed + ", queued=" + report.queued +
                      ", reviews=" + report.reviews +
                      ", recognized=" + report.recognized +
                      ", unknown=" + report.unknown +
                      ", skipped=" + report.skipped + ". Report: " + path);
        }

        public static void ReconcileAndAuditFromCommandLine()
        {
            RunFromCommandLine();
            SavicEditorContext context = SavicEditorContext.Instance;
            int ticks = 0;
            while (ticks < 128 &&
                   context.Batch.TickOneIgnoringCooldownForDiagnostics())
                ticks++;
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
            Debug.Log("[SAVIC] Verified classification reconciliation used " +
                      ticks + " serialized batch tick(s).");
        }
    }
}
