using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicModelProcessingProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Process First Archived 3D Model",
            false,
            112)]
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

            IReadOnlyList<SavicManifest> manifests =
                context.Manifests.GetAll();

            SavicManifest candidate = null;

            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest manifest = manifests[index];

                if (manifest?.source != null &&
                    string.Equals(
                        manifest.source.sourceKind,
                        SavicSourceKind.Model3D.ToString(),
                        StringComparison.Ordinal))
                {
                    candidate = manifest;
                    break;
                }
            }

            if (candidate == null)
            {
                throw new InvalidOperationException(
                    "SAVIC has no archived 3D model to process.");
            }

            SavicSourceProcessingOutcome outcome =
                context.SourceProcessing.Process(candidate);

            if (!outcome.Succeeded)
            {
                throw new InvalidOperationException(
                    "SAVIC model processing failed: " +
                    outcome.Message);
            }

            SavicManifest processed = outcome.Manifest;

            if (processed?.model3D == null ||
                !processed.model3D.analyzed ||
                !processed.model3D.hasUsableBounds ||
                processed.model3D.uniqueMeshCount <= 0)
            {
                throw new InvalidOperationException(
                    "SAVIC model analysis did not produce usable geometry.");
            }

            Debug.Log(
                "[SAVIC] REAL MODEL PROCESSING PROBE — PASS\n" +
                "SavicId: " + processed.savicId + "\n" +
                "Source: " + processed.source.originalFileName + "\n" +
                "Bounds: " +
                processed.model3D.widthMeters.ToString("0.###") + " x " +
                processed.model3D.heightMeters.ToString("0.###") + " x " +
                processed.model3D.depthMeters.ToString("0.###") + " m\n" +
                "Meshes: " + processed.model3D.uniqueMeshCount + "\n" +
                "Vertices: " + processed.model3D.vertexCount + "\n" +
                "Triangles: " + processed.model3D.triangleCount + "\n" +
                "Materials: " + processed.model3D.uniqueMaterialCount);
        }
    }
}
