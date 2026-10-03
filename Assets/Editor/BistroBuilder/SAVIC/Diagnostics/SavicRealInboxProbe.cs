using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicRealInboxProbe
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Ingest Current Inbox Now", false, 111)]
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
            SavicEditorContext context = SavicEditorContext.Instance;
            context.Layout.EnsureInfrastructure();

            string[] candidates = Directory.GetFiles(
                    context.Layout.DropHereRoot,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .Where(path =>
                {
                    string extension = Path.GetExtension(path);
                    return string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (candidates.Length == 0)
                throw new InvalidOperationException("SAVIC real inbox probe found no 3D candidates.");

            int ingested = 0;
            int duplicates = 0;

            foreach (string candidate in candidates)
            {
                SavicIntakeOutcome outcome =
                    context.Intake.IngestSynchronously(candidate);

                if (!outcome.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Intake failed for '" + Path.GetFileName(candidate) +
                        "': " + outcome.Message);
                }

                if (outcome.DuplicateExact)
                    duplicates++;
                else
                    ingested++;
            }

            if (Directory.GetFiles(context.Layout.DropHereRoot).Any(
                    path =>
                    {
                        string extension = Path.GetExtension(path);
                        return string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase);
                    }))
            {
                throw new InvalidOperationException(
                    "One or more 3D files remained in DropHere after synchronous intake.");
            }

            Debug.Log(
                "[SAVIC] REAL INBOX PROBE — PASS\n" +
                "Ingested: " + ingested + "\n" +
                "Exact duplicates: " + duplicates + "\n" +
                "Known manifests: " + context.Manifests.Count);
        }
    }
}
