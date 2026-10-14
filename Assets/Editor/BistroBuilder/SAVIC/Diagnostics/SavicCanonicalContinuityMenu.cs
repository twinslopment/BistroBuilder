using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicCanonicalContinuityMenu
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Continuity/Audit Canonical Sources", false, 121)]
        public static void AuditFromMenu()
        {
            AuditFromCommandLine();
        }

        public static void AuditFromCommandLine()
        {
            SavicEditorContext context = SavicEditorContext.Instance;
            SavicContinuityReport report = context.CanonicalReconciliation.Audit();
            string path = Path.Combine(context.Layout.LogsRoot, "canonical-continuity.json");
            SavicAtomicFile.WriteJson(path, report);
            Debug.Log("[SAVIC] Canonical continuity audit: " +
                      "ready=" + report.readyToReconcile +
                      ", managed=" + report.alreadyManaged +
                      ", source missing=" + report.sourceMissing +
                      ", source invalid=" + report.sourceInvalid +
                      ", processing failed=" + report.processingFailed +
                      ", identity conflict=" + report.identityConflict +
                      ". Report: " + path);
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Continuity/Reconcile Verified Sources", false, 122)]
        public static void ReconcileFromMenu()
        {
            SavicEditorContext context = SavicEditorContext.Instance;
            int enabled = context.CanonicalReconciliation.ReconcileVerifiedSources();
            Debug.Log("[SAVIC] Canonical continuity: " + enabled +
                      " verified legacy job(s) enabled. Missing or invalid originals were left untouched.");
            AuditFromCommandLine();
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Continuity/Attach Verified Original", false, 123)]
        public static void AttachFromMenu()
        {
            string path = EditorUtility.OpenFilePanel(
                "Select one original source file for SAVIC", string.Empty, string.Empty);
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                string name = SavicEditorContext.Instance.CanonicalReconciliation
                    .AttachVerifiedOriginal(path);
                Debug.Log("[SAVIC] Verified original attached and queued: " + name);
                AuditFromCommandLine();
            }
            catch (Exception exception)
            {
                Debug.LogError("[SAVIC] Original was not attached: " + exception);
            }
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Continuity/Retry Verified Mirror Failures", false, 124)]
        public static void RetryMirrorFailuresFromMenu()
        {
            int retried = SavicEditorContext.Instance.CanonicalReconciliation
                .RetryVerifiedMirrorFailures();
            Debug.Log("[SAVIC] Canonical continuity: " + retried +
                      " verified source-mirror failure(s) queued for retry.");
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Continuity/Import Selected GLB Folder", false, 125)]
        public static void ImportSelectedFolderFromMenu()
        {
            string folder = EditorUtility.OpenFolderPanel(
                "Select a folder containing only the GLB files for this SAVIC batch",
                string.Empty, string.Empty);
            if (string.IsNullOrWhiteSpace(folder))
                return;

            string[] files = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), ".glb",
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
            {
                Debug.LogWarning("[SAVIC] Selected folder contains no GLB files.");
                return;
            }

            SavicEditorContext context = SavicEditorContext.Instance;
            SavicSelectedImportResult result = context.CanonicalReconciliation
                .ImportSelectedFiles(files, context.Intake);
            Debug.Log("[SAVIC] Selected GLB import: restored=" + result.Restored +
                      ", new=" + result.Ingested +
                      ", duplicates=" + result.Duplicates +
                      ", errors=" + result.Errors.Count + ".");
            foreach (string error in result.Errors)
                Debug.LogError("[SAVIC] Selected GLB import: " + error);
            AuditFromCommandLine();
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Continuity/Retry Obsolete Pre-Import Reviews", false, 126)]
        public static void RetryObsoletePreImportReviewsFromMenu()
        {
            int retried = SavicEditorContext.Instance.CanonicalReconciliation
                .RetryObsoletePreImportReviews();
            Debug.Log("[SAVIC] Verified obsolete pre-import review(s) queued: " + retried + ".");
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Continuity/Retry Best Verified Chair", false, 127)]
        public static void RetryBestVerifiedChairFromMenu()
        {
            string name = SavicEditorContext.Instance.CanonicalReconciliation
                .RetryBestVerifiedChairAuthoringReview();
            if (string.IsNullOrEmpty(name))
                Debug.Log("[SAVIC] No verified chair review currently passes the safe authoring plan.");
            else
                Debug.Log("[SAVIC] Verified chair queued under the calibrated plan: " + name);
        }
    }
}
