using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicCanonicalContentInventoryProbe
    {
        [Serializable]
        private sealed class AuditReport
        {
            public int schemaVersion = 1;
            public string generatedUtc = string.Empty;
            public int totalUnique;
            public int inCatalog;
            public int published;
            public int needsReview;
            public int failed;
            public int inboxPending;
            public int orphaned;
            public List<AuditRow> rows =
                new List<AuditRow>();
        }

        [Serializable]
        private sealed class AuditRow
        {
            public string name = string.Empty;
            public string lifecycle = string.Empty;
            public string pipelineStatus = string.Empty;
            public string sourceLocation = string.Empty;
            public string catalogState = string.Empty;
            public string savicId = string.Empty;
            public string sourceHash = string.Empty;
            public string contentId = string.Empty;
            public string family = string.Empty;
            public string type = string.Empty;
            public string category = string.Empty;
            public string reason = string.Empty;
            public string archivedPath = string.Empty;
            public int artifactCount;
        }

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Canonical Content Inventory Audit",
            false,
            119)]
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
            SavicEditorContext context =
                SavicEditorContext.Instance;

            SavicProjectInventorySnapshot projectInventory =
                context.ProjectInventory.ScanAndPersist();

            SavicCanonicalContentInventorySnapshot inventory =
                SavicCanonicalContentInventoryService.Build(
                    context.Layout,
                    context.Manifests.GetAll(),
                    context.Jobs.Jobs,
                    projectInventory);

            if (inventory.Total <= 0)
            {
                throw new InvalidOperationException(
                    "Canonical content inventory found no content.");
            }

            AuditReport report =
                new AuditReport
                {
                    generatedUtc =
                        DateTime.UtcNow.ToString("O"),
                    totalUnique =
                        inventory.Total,
                    inCatalog =
                        inventory.InCatalog,
                    published =
                        inventory.Published,
                    needsReview =
                        inventory.NeedsReview,
                    failed =
                        inventory.Failed,
                    inboxPending =
                        inventory.InboxPending,
                    orphaned =
                        inventory.Orphaned
                };

            for (int index = 0;
                 index < inventory.Rows.Count;
                 index++)
            {
                SavicCanonicalContentInventoryRow row =
                    inventory.Rows[index];

                if (row == null)
                    continue;

                report.rows.Add(
                    new AuditRow
                    {
                        name =
                            row.DisplayName,
                        lifecycle =
                            row.Lifecycle,
                        pipelineStatus =
                            row.PipelineStatus,
                        sourceLocation =
                            row.SourceLocation,
                        catalogState =
                            row.CatalogState,
                        savicId =
                            row.SavicId,
                        sourceHash =
                            row.SourceHash,
                        contentId =
                            row.CanonicalContentId,
                        family =
                            row.Family,
                        type =
                            row.Type,
                        category =
                            row.Category,
                        reason =
                            row.Reason,
                        archivedPath =
                            row.ArchivedRelativePath,
                        artifactCount =
                            row.ArtifactPaths.Count
                    });
            }

            string reportPath =
                Path.Combine(
                    context.Layout.LogsRoot,
                    "canonical-content-inventory.json");

            SavicAtomicFile.WriteJson(
                reportPath,
                report);

            Debug.Log(
                "[SAVIC] CANONICAL CONTENT INVENTORY AUDIT - COMPLETE\n" +
                "Total unique: " + inventory.Total + "\n" +
                "In catalog: " + inventory.InCatalog + "\n" +
                "Published: " + inventory.Published + "\n" +
                "Needs review: " + inventory.NeedsReview + "\n" +
                "Failed: " + inventory.Failed + "\n" +
                "Inbox pending: " + inventory.InboxPending + "\n" +
                "Orphaned: " + inventory.Orphaned + "\n" +
                "Report: " + reportPath);

            for (int index = 0;
                 index < inventory.Rows.Count;
                 index++)
            {
                SavicCanonicalContentInventoryRow row =
                    inventory.Rows[index];

                if (row == null)
                    continue;

                Debug.Log(
                    "[SAVIC][INVENTORY] " +
                    row.DisplayName +
                    " | " +
                    row.Lifecycle +
                    " | " +
                    row.PipelineStatus +
                    " | " +
                    row.SourceLocation +
                    " | " +
                    row.CatalogState +
                    (string.IsNullOrWhiteSpace(row.Reason)
                        ? string.Empty
                        : " | " + row.Reason));
            }
        }
    }
}
