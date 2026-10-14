using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicDiagnostics
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Report Status", false, 100)]
        private static void ReportStatus()
        {
            SavicEditorContext context = SavicEditorContext.Instance;
            context.Layout.EnsureInfrastructure();

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("BISTRO BUILDER — SAVIC STATUS");
            builder.AppendLine("============================");
            builder.AppendLine("Version: " + SavicVersion.ProductVersion);
            builder.AppendLine("Pipeline: " + SavicVersion.PipelineVersion);
            builder.AppendLine("Project: " + context.Layout.ProjectRoot);
            builder.AppendLine("DropHere: " + context.Layout.DropHereRoot);
            builder.AppendLine("Inbox candidates: " + context.Intake.GetInboxFileCount());
            builder.AppendLine("Known manifests: " + context.Manifests.Count);
            builder.AppendLine(
                "Ingested jobs: " +
                context.Jobs.CountByState(SavicJobState.Ingested));
            builder.AppendLine(
                "Exact duplicates: " +
                context.Jobs.CountByState(SavicJobState.DuplicateExact));
            builder.AppendLine(
                "Quarantined: " +
                context.Jobs.CountByState(SavicJobState.Quarantined));
            builder.AppendLine(
                "Intake busy: " +
                context.Intake.IsBusy +
                (context.Intake.IsBusy
                    ? " (" + context.Intake.PendingFileName + ")"
                    : string.Empty));

            if (!string.IsNullOrWhiteSpace(SavicEditorBootstrap.LastInitializationError))
            {
                builder.AppendLine(
                    "Initialization error: " +
                    SavicEditorBootstrap.LastInitializationError);
            }

            Debug.Log(builder.ToString());
        }
    }
}
