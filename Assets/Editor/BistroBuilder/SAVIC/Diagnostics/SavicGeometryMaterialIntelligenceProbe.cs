using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicGeometryMaterialIntelligenceProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Geometry Material Intelligence Probe",
            false,
            117)]
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

            SavicManifest source =
                FindPublishedTable(
                    context.Manifests.GetAll());

            Require(
                source != null,
                "No published SAVIC table is available for intelligence validation.");

            Require(
                source.model3D != null &&
                source.model3D.geometry != null &&
                source.model3D.geometry.analyzed &&
                source.model3D.geometry.usable,
                "Published table has no usable geometry profile.");

            Require(
                source.model3D.geometry.sourceTriangleCount > 0 &&
                source.model3D.geometry.sampledTriangleCount > 0,
                "Geometry profile contains no sampled triangle evidence.");

            SavicManifest neutralName =
                CloneManifest(source);

            neutralName.source.originalFileName =
                "asset_0042.glb";

            SavicClassificationRecord geometryOnly =
                SavicContentClassifier.Classify(
                    neutralName);

            Require(
                string.Equals(
                    geometryOnly.type,
                    "Table",
                    StringComparison.Ordinal),
                "Geometry-backed classifier could not identify the table without a helpful filename.");

            Require(
                geometryOnly.geometryBacked,
                "Neutral-name classification is not geometry-backed.");

            Require(
                !geometryOnly.explicitTypeToken &&
                !geometryOnly.nameBacked,
                "Neutral-name classification still depends on a table name token.");

            Require(
                geometryOnly.score >= 0.78f,
                "Geometry-only table score is below automatic-authoring threshold.");

            neutralName.classification =
                geometryOnly;

            SavicResolvedMaterialSemantic neutralMaterial =
                SavicMaterialSemanticResolver.Resolve(
                    neutralName);

            Require(
                !neutralMaterial.IsKnown,
                "SAVIC invented a material semantic after all trustworthy material-name evidence was removed.");

            SavicManifest conflictingName =
                CloneManifest(source);

            conflictingName.source.originalFileName =
                "restaurant_chair_asset.glb";

            SavicClassificationRecord conflict =
                SavicContentClassifier.Classify(
                    conflictingName);

            Require(
                !string.Equals(
                    conflict.type,
                    "Table",
                    StringComparison.Ordinal),
                "Conflicting chair metadata was ignored instead of routing the asset away from automatic table publication.");

            SavicResolvedMaterialSemantic originalMaterial =
                SavicMaterialSemanticResolver.Resolve(
                    source);

            Require(
                originalMaterial.IsKnown,
                "Original table material semantic was not resolved.");

            Require(
                string.Equals(
                    originalMaterial.Source,
                    "MATERIAL_DATA",
                    StringComparison.Ordinal) ||
                string.Equals(
                    originalMaterial.Source,
                    "SOURCE_METADATA_FALLBACK",
                    StringComparison.Ordinal),
                "Resolved material semantic has no auditable evidence source.");

            Debug.Log(
                "[SAVIC] GEOMETRY MATERIAL INTELLIGENCE PROBE - PASS\n" +
                "Geometry-only type: " +
                geometryOnly.type +
                "\nGeometry-only score: " +
                geometryOnly.score.ToString("0.000") +
                "\nGeometry-only evidence: " +
                geometryOnly.evidence +
                "\nNeutral-name material: " +
                neutralMaterial.Semantic +
                " (" +
                neutralMaterial.Source +
                ")" +
                "\nOriginal material: " +
                originalMaterial.Semantic +
                " (" +
                originalMaterial.Source +
                ")" +
                "\nUV0 present: " +
                source.model3D.hasUv0 +
                "\nVertex colors present: " +
                source.model3D.hasVertexColors +
                "\nSampled triangles: " +
                source.model3D.geometry.sampledTriangleCount);
        }

        private static SavicManifest FindPublishedTable(
            System.Collections.Generic.IReadOnlyList
                <SavicManifest> manifests)
        {
            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest candidate =
                    manifests[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.status,
                        "PUBLISHED",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static SavicManifest CloneManifest(
            SavicManifest source)
        {
            string json =
                JsonUtility.ToJson(
                    source,
                    false);

            SavicManifest clone =
                JsonUtility.FromJson<SavicManifest>(
                    json);

            if (clone == null)
            {
                throw new InvalidOperationException(
                    "Manifest clone failed.");
            }

            return clone;
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
