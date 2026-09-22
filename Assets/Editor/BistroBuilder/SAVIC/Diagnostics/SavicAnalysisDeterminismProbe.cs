using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicAnalysisDeterminismProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Analysis Determinism Probe",
            false,
            120)]
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
            CultureInfo originalCulture =
                CultureInfo.CurrentCulture;

            CultureInfo originalUiCulture =
                CultureInfo.CurrentUICulture;

            try
            {
                GameObject sourceModel =
                    LoadPublishedTableSourceModel(
                        out SavicManifest sourceManifest);

                AnalysisSnapshot en =
                    AnalyzeUnderCulture(
                        sourceModel,
                        sourceManifest,
                        new CultureInfo("en-US"));

                AnalysisSnapshot es =
                    AnalyzeUnderCulture(
                        sourceModel,
                        sourceManifest,
                        new CultureInfo("es-ES"));

                RequireEquivalent(
                    en,
                    es);

                Debug.Log(
                    "[SAVIC] ANALYSIS DETERMINISM PROBE - PASS\n" +
                    "Geometry evidence culture invariant: " +
                    en.GeometryEvidence +
                    "\nClassification: " +
                    en.ClassificationType +
                    " / " +
                    en.ClassificationScore.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture) +
                    "\nMaterial: " +
                    en.MaterialSemantic +
                    " / " +
                    en.MaterialSource);
            }
            finally
            {
                CultureInfo.CurrentCulture =
                    originalCulture;

                CultureInfo.CurrentUICulture =
                    originalUiCulture;
            }
        }

        private static AnalysisSnapshot AnalyzeUnderCulture(
            GameObject sourceModel,
            SavicManifest sourceManifest,
            CultureInfo culture)
        {
            CultureInfo.CurrentCulture =
                culture;

            CultureInfo.CurrentUICulture =
                culture;

            SavicModelAnalysisRecord analysis =
                SavicModelAnalyzer.Analyze(
                    sourceModel);

            SavicManifest manifest =
                CloneManifest(
                    sourceManifest);

            manifest.model3D =
                analysis;

            SavicClassificationRecord classification =
                SavicContentClassifier.Classify(
                    manifest);

            manifest.classification =
                classification;

            manifest.family =
                classification.family;

            manifest.type =
                classification.type;

            manifest.category =
                classification.category;

            SavicResolvedMaterialSemantic material =
                SavicMaterialSemanticResolver.Resolve(
                    manifest);

            return new AnalysisSnapshot
            {
                Width = analysis.widthMeters,
                Height = analysis.heightMeters,
                Depth = analysis.depthMeters,
                TriangleCount = analysis.triangleCount,
                GeometryEvidence =
                    analysis.geometry?.evidence ??
                    string.Empty,
                UpwardArea =
                    analysis.geometry?.upwardFacingAreaRatio ??
                    0f,
                UpperBandArea =
                    analysis.geometry?.upperBandAreaRatio ??
                    0f,
                LowerBandArea =
                    analysis.geometry?.lowerBandAreaRatio ??
                    0f,
                SurfaceCentroid =
                    analysis.geometry?.surfaceAreaCentroidHeight01 ??
                    0f,
                UpperCoverage =
                    analysis.geometry?.upperUpwardProjectedCoverage ??
                    0f,
                ClassificationType =
                    classification.type,
                ClassificationScore =
                    classification.score,
                ClassificationEvidence =
                    classification.evidence,
                MaterialSemantic =
                    material.Semantic,
                MaterialConfidence =
                    material.Confidence,
                MaterialSource =
                    material.Source,
                MaterialEvidence =
                    material.Evidence
            };
        }

        private static GameObject LoadPublishedTableSourceModel(
            out SavicManifest manifest)
        {
            SavicEditorContext context =
                SavicEditorContext.Instance;

            System.Collections.Generic.IReadOnlyList
                <SavicManifest> manifests =
                    context.Manifests.GetAll();

            manifest = null;

            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest candidate =
                    manifests[index];

                if (candidate == null ||
                    !string.Equals(
                        candidate.status,
                        "PUBLISHED",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                manifest =
                    candidate;
                break;
            }

            Require(
                manifest != null,
                "No published SAVIC table exists for determinism validation.");

            SavicArtifactRecord sourceArtifact =
                FindArtifact(
                    manifest,
                    "unity.source_mirror");

            Require(
                sourceArtifact != null &&
                !string.IsNullOrWhiteSpace(
                    sourceArtifact.projectRelativePath),
                "Published table has no Unity source-mirror artifact.");

            GameObject model =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    sourceArtifact.projectRelativePath);

            Require(
                model != null,
                "Published table source mirror could not be loaded.");

            return model;
        }

        private static SavicArtifactRecord FindArtifact(
            SavicManifest manifest,
            string role)
        {
            if (manifest?.artifacts == null)
                return null;

            for (int index = 0;
                 index < manifest.artifacts.Count;
                 index++)
            {
                SavicArtifactRecord artifact =
                    manifest.artifacts[index];

                if (artifact != null &&
                    string.Equals(
                        artifact.role,
                        role,
                        StringComparison.Ordinal))
                {
                    return artifact;
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

            Require(
                clone != null,
                "Manifest clone failed.");

            return clone;
        }

        private static void RequireEquivalent(
            AnalysisSnapshot left,
            AnalysisSnapshot right)
        {
            Require(
                BitConverter.SingleToInt32Bits(left.Width) ==
                BitConverter.SingleToInt32Bits(right.Width) &&
                BitConverter.SingleToInt32Bits(left.Height) ==
                BitConverter.SingleToInt32Bits(right.Height) &&
                BitConverter.SingleToInt32Bits(left.Depth) ==
                BitConverter.SingleToInt32Bits(right.Depth),
                "Model bounds changed with CurrentCulture.");

            Require(
                left.TriangleCount ==
                right.TriangleCount,
                "Triangle count changed with CurrentCulture.");

            RequireSameFloat(
                left.UpwardArea,
                right.UpwardArea,
                "upward-area ratio");

            RequireSameFloat(
                left.UpperBandArea,
                right.UpperBandArea,
                "upper-band ratio");

            RequireSameFloat(
                left.LowerBandArea,
                right.LowerBandArea,
                "lower-band ratio");

            RequireSameFloat(
                left.SurfaceCentroid,
                right.SurfaceCentroid,
                "surface-centroid ratio");

            RequireSameFloat(
                left.UpperCoverage,
                right.UpperCoverage,
                "upper projected coverage");

            Require(
                string.Equals(
                    left.GeometryEvidence,
                    right.GeometryEvidence,
                    StringComparison.Ordinal),
                "Geometry evidence serialization depends on CurrentCulture.");

            Require(
                string.Equals(
                    left.ClassificationType,
                    right.ClassificationType,
                    StringComparison.Ordinal) &&
                BitConverter.SingleToInt32Bits(
                    left.ClassificationScore) ==
                BitConverter.SingleToInt32Bits(
                    right.ClassificationScore) &&
                string.Equals(
                    left.ClassificationEvidence,
                    right.ClassificationEvidence,
                    StringComparison.Ordinal),
                "Content classification depends on CurrentCulture.");

            Require(
                string.Equals(
                    left.MaterialSemantic,
                    right.MaterialSemantic,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.MaterialConfidence,
                    right.MaterialConfidence,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.MaterialSource,
                    right.MaterialSource,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.MaterialEvidence,
                    right.MaterialEvidence,
                    StringComparison.Ordinal),
                "Material semantic resolution depends on CurrentCulture.");
        }

        private static void RequireSameFloat(
            float left,
            float right,
            string label)
        {
            Require(
                BitConverter.SingleToInt32Bits(left) ==
                BitConverter.SingleToInt32Bits(right),
                label +
                " changed with CurrentCulture.");
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class AnalysisSnapshot
        {
            public float Width;
            public float Height;
            public float Depth;
            public long TriangleCount;
            public string GeometryEvidence = string.Empty;
            public float UpwardArea;
            public float UpperBandArea;
            public float LowerBandArea;
            public float SurfaceCentroid;
            public float UpperCoverage;
            public string ClassificationType = string.Empty;
            public float ClassificationScore;
            public string ClassificationEvidence = string.Empty;
            public string MaterialSemantic = string.Empty;
            public string MaterialConfidence = string.Empty;
            public string MaterialSource = string.Empty;
            public string MaterialEvidence = string.Empty;
        }
    }
}
