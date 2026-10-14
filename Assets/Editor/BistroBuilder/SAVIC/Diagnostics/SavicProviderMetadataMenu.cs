using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicProviderMetadataMenu
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Continuity/Attach Verified Provider Metadata", false, 133)]
        public static void RunFromMenu()
        {
            string path = EditorUtility.OpenFilePanel("Select provider metadata linked to downloaded originals", "", "json");
            if (!string.IsNullOrWhiteSpace(path))
                Import(path);
        }

        public static void ImportAndReconcileFromCommandLine()
        {
            SavicProviderMetadataSelfTest.RunFromCommandLine();
            SavicAutonomousClassificationSelfTest.RunFromCommandLine();
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-savicProviderMetadata");
            if (index < 0 || index + 1 >= args.Length)
                throw new InvalidOperationException("Supply -savicProviderMetadata with the verified provider metadata JSON path.");
            Import(args[index + 1]);
            SavicAutonomousClassificationAudit.ReconcileAndAuditFromCommandLine();
        }

        private static void Import(string path)
        {
            SavicProviderMetadataBatch batch = SavicAtomicFile.ReadJson<SavicProviderMetadataBatch>(path);
            if (batch?.schemaVersion != 1 || batch.records == null || batch.records.Count > 64)
                throw new InvalidOperationException("Unsupported or oversized provider metadata batch.");
            SavicEditorContext context = SavicEditorContext.Instance;
            int attached = 0, unchanged = 0, rejected = 0;
            foreach (SavicProviderMetadataInput input in batch.records)
            {
                try
                {
                    if (SavicProviderMetadataService.AttachVerified(context.Layout, context.Manifests, input))
                        attached++;
                    else
                        unchanged++;
                }
                catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                                                   exception is InvalidOperationException || exception is UnauthorizedAccessException)
                {
                    rejected++;
                    Debug.LogWarning("[SAVIC] Provider metadata rejected: " + exception.Message);
                }
            }
            Debug.Log("[SAVIC] Verified provider metadata: attached=" + attached + ", unchanged=" + unchanged + ", rejected=" + rejected + ".");
            if (rejected > 0 && Application.isBatchMode)
                throw new InvalidOperationException("Provider metadata batch contains rejected records; inspect the report before continuing.");
        }
    }
}
