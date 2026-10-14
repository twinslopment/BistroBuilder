using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicMaterialSemanticsProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Material Semantics Probe",
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

            SavicManifest table =
                FindTable(
                    context.Manifests.GetAll());

            Require(
                table != null,
                "No analyzed SAVIC table is available.");

            SavicModelAnalysisRecord analysis =
                table.model3D;

            Require(
                analysis != null &&
                analysis.analyzed,
                "SAVIC table has no model analysis.");

            Require(
                string.Equals(
                    analysis.dominantMaterialSemantic,
                    "Unknown",
                    StringComparison.Ordinal),
                "Raw Meshy material unexpectedly contains a semantic label.");

            Require(
                string.Equals(
                    analysis.appearanceDataCompleteness,
                    "NONE",
                    StringComparison.Ordinal),
                "Raw Meshy source unexpectedly contains authored appearance data.");

            SavicResolvedMaterialSemantic resolved =
                SavicMaterialSemanticResolver.Resolve(
                    table);

            Require(
                resolved.IsKnown,
                "Source metadata fallback did not resolve the explicit material token.");

            Require(
                string.Equals(
                    resolved.Semantic,
                    "Wood",
                    StringComparison.Ordinal),
                "Explicit Wooden source metadata did not resolve to Wood.");

            Require(
                string.Equals(
                    resolved.Confidence,
                    "MEDIUM",
                    StringComparison.Ordinal),
                "Filename-only material fallback must remain MEDIUM confidence.");

            Require(
                string.Equals(
                    resolved.Source,
                    "SOURCE_METADATA_FALLBACK",
                    StringComparison.Ordinal),
                "Material fallback did not preserve its provenance.");

            SavicManifest genericClone =
                CloneManifest(
                    table);

            genericClone.source.originalFileName =
                "asset_000001.glb";

            SavicResolvedMaterialSemantic genericResolved =
                SavicMaterialSemanticResolver.Resolve(
                    genericClone);

            Require(
                !genericResolved.IsKnown &&
                string.Equals(
                    genericResolved.Semantic,
                    "Unknown",
                    StringComparison.Ordinal),
                "Material resolver invented a semantic without evidence.");

            Debug.Log(
                "[SAVIC] MATERIAL SEMANTICS PROBE - PASS\n" +
                "Raw semantic: " +
                analysis.dominantMaterialSemantic +
                "\nAppearance payload: " +
                analysis.appearanceDataCompleteness +
                "\nResolved semantic: " +
                resolved.Semantic +
                " / " +
                resolved.Confidence +
                " / " +
                resolved.Source +
                "\nGeneric filename semantic: " +
                genericResolved.Semantic +
                "\nNo unsupported visual material detail was invented.");
        }

        private static SavicManifest FindTable(
            IReadOnlyList<SavicManifest> manifests)
        {
            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest candidate =
                    manifests[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal) &&
                    candidate.model3D != null &&
                    candidate.model3D.analyzed)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static SavicManifest CloneManifest(
            SavicManifest manifest)
        {
            string json =
                JsonUtility.ToJson(
                    manifest,
                    false);

            return JsonUtility.FromJson<SavicManifest>(
                json);
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
