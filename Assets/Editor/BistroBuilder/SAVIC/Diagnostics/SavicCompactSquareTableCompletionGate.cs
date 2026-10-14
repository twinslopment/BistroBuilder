using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicCompactSquareTableCompletionGate
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Compact Square Table Completion Gate", false, 134)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void RunFromCommandLine()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException(
                    "Table publication requires a graphics device.");

            SavicCompactSquareTableProbe.RunFromCommandLine();
            SavicEditorContext context = SavicEditorContext.Instance;
            if (context.Jobs.IsPaused)
                throw new InvalidOperationException("SAVIC queue is paused.");

            int queued = context.CanonicalReconciliation
                .RetryVerifiedCompactSquareTableReviews(4);
            SavicJobRecord[] targets = context.Jobs.Jobs
                .Where(job => job != null &&
                    job.lastAutomaticTablePlannerRetryVersion ==
                    SavicTableAuthoringPlanner.Version)
                .ToArray();
            if (targets.Length == 0)
                throw new InvalidOperationException(
                    "No verified compact square table was selected by this planner revision.");

            for (int tick = 0; tick < 512; tick++)
            {
                if (targets.All(job =>
                    job.state != SavicJobState.Ingested.ToString() &&
                    job.state != SavicJobState.Processing.ToString()))
                    break;

                if (!context.Batch.TickOneIgnoringCooldownForDiagnostics())
                    throw new InvalidOperationException(
                        "SAVIC could not advance the verified compact square table.");
            }

            targets = context.Jobs.Jobs
                .Where(job => job != null &&
                    job.lastAutomaticTablePlannerRetryVersion ==
                    SavicTableAuthoringPlanner.Version)
                .ToArray();
            foreach (SavicJobRecord job in targets)
            {
                if (job.state != SavicJobState.Done.ToString() ||
                    job.reasonCode != "PUBLISHED")
                    throw new InvalidOperationException(
                        "Compact square table did not publish: " +
                        job.originalFileName + " " + job.state + "/" +
                        job.reasonCode + " - " + job.message);

                if (!context.Manifests.TryGetBySavicId(job.manifestSavicId,
                        out SavicManifest manifest) ||
                    manifest.status != "PUBLISHED" ||
                    manifest.tableAuthoring == null ||
                    manifest.tableAuthoring.seatingDefinitionAssetPath !=
                    SavicTableAuthoringPlanner.CompactSquareTwoSeatingPath ||
                    manifest.tableSpatial == null ||
                    !manifest.tableSpatial.validated ||
                    !manifest.tableSpatial.runtimeBindingValidated ||
                    manifest.tableSpatial.configurationId !=
                        "table_compact_2_rectangular" ||
                    manifest.tableSpatial.emittedSeatBayCount != 2 ||
                    manifest.tableNavigation == null ||
                    !manifest.tableNavigation.validated ||
                    manifest.tablePersistence == null ||
                    !manifest.tablePersistence.validated ||
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        manifest.tableAuthoring.prefabAssetPath) == null ||
                    AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                        manifest.tableAuthoring.itemDefinitionAssetPath) == null)
                    throw new InvalidOperationException(
                        "Published compact table failed catalog, BBSIS, navigation or persistence checks: " +
                        job.originalFileName);

                string largePreviewPath = manifest.artifacts
                    .FirstOrDefault(artifact => artifact != null &&
                        artifact.role == "preview.large")?.projectRelativePath;
                string catalogPreviewPath = manifest.artifacts
                    .FirstOrDefault(artifact => artifact != null &&
                        artifact.role == "preview.catalog")?.projectRelativePath;
                if (string.IsNullOrWhiteSpace(largePreviewPath) ||
                    string.IsNullOrWhiteSpace(catalogPreviewPath) ||
                    AssetDatabase.LoadAssetAtPath<Texture2D>(largePreviewPath) == null ||
                    AssetDatabase.LoadAssetAtPath<Texture2D>(catalogPreviewPath) == null)
                    throw new InvalidOperationException(
                        "Published compact table is missing a preview: " +
                        job.originalFileName);

                SavicTableSaveLoadStateProbe.RunTableForSavicId(job.manifestSavicId);
            }
            Debug.Log("[SAVIC] COMPACT SQUARE TABLE COMPLETION GATE - PASS: " +
                      "queued=" + queued + ", verified published=" +
                      targets.Length + ", BBSIS=PASS, navigation=PASS, " +
                      "persistence=PASS, SaveLoad=PASS.");
        }
    }
}
