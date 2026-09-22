using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicSemanticPartProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Semantic Part Probe",
            false,
            121)]
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
            ValidateRealPublishedTable();
            ValidateMultiMeshGrouping();
            ValidateQuantizationBoundaryWeld();

            Debug.Log(
                "[SAVIC] SEMANTIC PART PROBE - PASS\n" +
                "Real Meshy table: topology + semantic zones stable.\n" +
                "Synthetic multi-mesh table: four independent supports grouped into one semantic LegSet.\n" +
                "Near-coincident topology: vertices across quantization cells weld into one physical region.");
        }

        private static void ValidateRealPublishedTable()
        {
            SavicEditorContext context =
                SavicEditorContext.Instance;

            SavicManifest manifest =
                FindPublishedTable(
                    context.Manifests.GetAll());

            Require(
                manifest != null,
                "No published SAVIC table exists for semantic-part validation.");

            SavicArtifactRecord sourceArtifact =
                FindArtifact(
                    manifest,
                    "unity.source_mirror");

            Require(
                sourceArtifact != null &&
                !string.IsNullOrWhiteSpace(
                    sourceArtifact.projectRelativePath),
                "Published table has no source mirror.");

            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    sourceArtifact.projectRelativePath);

            Require(
                source != null,
                "Published table source mirror could not be loaded.");

            SavicModelAnalysisRecord firstModel =
                SavicModelAnalyzer.Analyze(
                    source);

            SavicClassificationRecord classification =
                SavicContentClassifier.Classify(
                    BuildClassificationManifest(
                        manifest,
                        firstModel));

            Require(
                string.Equals(
                    classification.type,
                    "Table",
                    StringComparison.Ordinal),
                "Real table is no longer classified as Table.");

            SavicSemanticPartAnalysisRecord first =
                SavicSemanticPartAnalyzer.Analyze(
                    source,
                    firstModel,
                    classification);

            SavicModelAnalysisRecord secondModel =
                SavicModelAnalyzer.Analyze(
                    source);

            SavicSemanticPartAnalysisRecord second =
                SavicSemanticPartAnalyzer.Analyze(
                    source,
                    secondModel,
                    classification);

            RequireTableStructure(
                first,
                "real Meshy table");

            RequireDeterministic(
                first,
                second);

            Require(
                first.rawRegionCount >= 1,
                "Real table produced no physical source regions.");

            Require(
                first.supportPattern.zoneCount >= 3,
                "Real table support pattern did not preserve its multi-contact structure.");
        }

        private static void ValidateMultiMeshGrouping()
        {
            GameObject root =
                new GameObject(
                    "SyntheticSemanticTable");

            try
            {
                AddCube(
                    root.transform,
                    "Top",
                    new Vector3(
                        0f,
                        0.74f,
                        0f),
                    new Vector3(
                        1.40f,
                        0.08f,
                        0.80f));

                AddCube(
                    root.transform,
                    "Leg_NW",
                    new Vector3(
                        -0.58f,
                        0.36f,
                        0.28f),
                    new Vector3(
                        0.08f,
                        0.72f,
                        0.08f));

                AddCube(
                    root.transform,
                    "Leg_NE",
                    new Vector3(
                        0.58f,
                        0.36f,
                        0.28f),
                    new Vector3(
                        0.08f,
                        0.72f,
                        0.08f));

                AddCube(
                    root.transform,
                    "Leg_SW",
                    new Vector3(
                        -0.58f,
                        0.36f,
                        -0.28f),
                    new Vector3(
                        0.08f,
                        0.72f,
                        0.08f));

                AddCube(
                    root.transform,
                    "Leg_SE",
                    new Vector3(
                        0.58f,
                        0.36f,
                        -0.28f),
                    new Vector3(
                        0.08f,
                        0.72f,
                        0.08f));

                SavicModelAnalysisRecord model =
                    SavicModelAnalyzer.Analyze(
                        root);

                SavicClassificationRecord classification =
                    new SavicClassificationRecord
                    {
                        classified = true,
                        classifierVersion =
                            SavicContentClassifier.Version,
                        family = "Furniture",
                        type = "Table",
                        category = "Tables",
                        confidence = "HIGH",
                        score = 1f,
                        geometryBacked = true,
                        evidence =
                            "Synthetic diagnostic table contract."
                    };

                SavicSemanticPartAnalysisRecord semantic =
                    SavicSemanticPartAnalyzer.Analyze(
                        root,
                        model,
                        classification);

                RequireTableStructure(
                    semantic,
                    "synthetic multi-mesh table");

                Require(
                    semantic.rawRegionCount >= 5,
                    "Independent synthetic meshes were not preserved as physical source regions.");

                SavicSemanticPartRecord support =
                    FindPart(
                        semantic,
                        "table.support");

                Require(
                    support != null &&
                    support.sourceRegionCount >= 4,
                    "Multiple physical support meshes were not grouped into one semantic support part.");

                Require(
                    string.Equals(
                        support.role,
                        "LegSet",
                        StringComparison.Ordinal),
                    "Four discrete support contacts were not recognized as a LegSet.");

                Require(
                    semantic.supportPattern.zoneCount == 4,
                    "Synthetic four-leg table did not produce four support contact zones.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    root);
            }
        }

        private static void ValidateQuantizationBoundaryWeld()
        {
            GameObject root =
                new GameObject(
                    "SemanticWeldBoundaryTable");

            Mesh topMesh =
                null;

            try
            {
                GameObject top =
                    new GameObject(
                        "SplitTop");

                top.transform.SetParent(
                    root.transform,
                    false);

                MeshFilter filter =
                    top.AddComponent<MeshFilter>();

                top.AddComponent<MeshRenderer>();

                topMesh =
                    BuildSplitTopMesh();

                filter.sharedMesh =
                    topMesh;

                AddCube(
                    root.transform,
                    "CentralSupport",
                    new Vector3(
                        0f,
                        0.36f,
                        0f),
                    new Vector3(
                        0.18f,
                        0.72f,
                        0.18f));

                SavicModelAnalysisRecord model =
                    SavicModelAnalyzer.Analyze(
                        root);

                SavicClassificationRecord classification =
                    new SavicClassificationRecord
                    {
                        classified = true,
                        classifierVersion =
                            SavicContentClassifier.Version,
                        family = "Furniture",
                        type = "Table",
                        category = "Tables",
                        confidence = "HIGH",
                        score = 1f,
                        geometryBacked = true,
                        evidence =
                            "Quantization-boundary topology regression."
                    };

                SavicSemanticPartAnalysisRecord semantic =
                    SavicSemanticPartAnalyzer.Analyze(
                        root,
                        model,
                        classification);

                Require(
                    semantic.analyzed,
                    "Quantization-boundary semantic analysis did not complete.");

                Require(
                    semantic.rawRegionCount == 2,
                    "Near-coincident tabletop halves were split into separate physical regions. " +
                    "Expected 2 total regions (tabletop + support), got " +
                    semantic.rawRegionCount +
                    ".");
            }
            finally
            {
                if (topMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        topMesh);
                }

                UnityEngine.Object.DestroyImmediate(
                    root);
            }
        }

        private static Mesh BuildSplitTopMesh()
        {
            const float seamLeftX = 0.000008f;
            const float seamRightX = 0.000012f;
            const float y = 0.75f;
            const float minX = -0.70f;
            const float maxX = 0.70f;
            const float minZ = -0.40f;
            const float maxZ = 0.40f;

            Vector3[] vertices =
            {
                new Vector3(minX, y, minZ),
                new Vector3(seamLeftX, y, minZ),
                new Vector3(seamLeftX, y, maxZ),
                new Vector3(minX, y, maxZ),

                new Vector3(seamRightX, y, minZ),
                new Vector3(maxX, y, minZ),
                new Vector3(maxX, y, maxZ),
                new Vector3(seamRightX, y, maxZ)
            };

            int[] triangles =
            {
                0, 2, 1,
                0, 3, 2,
                4, 6, 5,
                4, 7, 6
            };

            Mesh mesh =
                new Mesh
                {
                    name =
                        "SAVIC_SemanticBoundaryWeldTop"
                };

            mesh.vertices =
                vertices;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();

            return mesh;
        }

        private static void AddCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject cube =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            cube.name =
                name;

            cube.transform.SetParent(
                parent,
                false);

            cube.transform.localPosition =
                localPosition;

            cube.transform.localRotation =
                Quaternion.identity;

            cube.transform.localScale =
                localScale;

            Collider collider =
                cube.GetComponent<Collider>();

            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    collider);
            }
        }

        private static void RequireTableStructure(
            SavicSemanticPartAnalysisRecord semantic,
            string label)
        {
            Require(
                semantic != null &&
                semantic.analyzed,
                label +
                " semantic analysis did not complete.");

            Require(
                semantic.automationReady,
                label +
                " semantic structure is not safe for automation: " +
                semantic.evidence);

            Require(
                semantic.semanticCoverage >= 0.95f,
                label +
                " has insufficient semantic surface coverage.");

            SavicSemanticPartRecord tabletop =
                FindPart(
                    semantic,
                    "table.top");

            SavicSemanticPartRecord support =
                FindPart(
                    semantic,
                    "table.support");

            Require(
                tabletop != null &&
                string.Equals(
                    tabletop.role,
                    "Tabletop",
                    StringComparison.Ordinal) &&
                tabletop.confidenceScore >= 0.62f,
                label +
                " has no reliable tabletop.");

            Require(
                support != null &&
                support.confidenceScore >= 0.62f,
                label +
                " has no reliable support structure.");

            Require(
                HasRelation(
                    semantic,
                    "table.support",
                    "table.top",
                    "SUPPORTS"),
                label +
                " is missing SUPPORTS relation.");

            Require(
                HasRelation(
                    semantic,
                    "table.top",
                    "table.support",
                    "SUPPORTED_BY"),
                label +
                " is missing SUPPORTED_BY relation.");
        }

        private static void RequireDeterministic(
            SavicSemanticPartAnalysisRecord first,
            SavicSemanticPartAnalysisRecord second)
        {
            Require(
                first.rawRegionCount ==
                second.rawRegionCount &&
                first.semanticPartCount ==
                second.semanticPartCount &&
                BitConverter.SingleToInt32Bits(
                    first.semanticCoverage) ==
                BitConverter.SingleToInt32Bits(
                    second.semanticCoverage),
                "Semantic-part aggregate output is not deterministic.");

            Require(
                first.parts.Count ==
                second.parts.Count,
                "Semantic-part count changed between identical analyses.");

            for (int index = 0;
                 index < first.parts.Count;
                 index++)
            {
                SavicSemanticPartRecord left =
                    first.parts[index];

                SavicSemanticPartRecord right =
                    second.parts[index];

                Require(
                    string.Equals(
                        left.partId,
                        right.partId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        left.role,
                        right.role,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        left.sourceRegionKeys,
                        right.sourceRegionKeys,
                        StringComparison.Ordinal) &&
                    BitConverter.SingleToInt32Bits(
                        left.confidenceScore) ==
                    BitConverter.SingleToInt32Bits(
                        right.confidenceScore),
                    "Semantic part identity or confidence changed between identical analyses.");
            }

            Require(
                string.Equals(
                    first.supportPattern.mode,
                    second.supportPattern.mode,
                    StringComparison.Ordinal) &&
                first.supportPattern.zoneCount ==
                second.supportPattern.zoneCount &&
                BitConverter.SingleToInt32Bits(
                    first.supportPattern.confidenceScore) ==
                BitConverter.SingleToInt32Bits(
                    second.supportPattern.confidenceScore),
                "Support-pattern analysis is not deterministic.");
        }

        private static SavicSemanticPartRecord FindPart(
            SavicSemanticPartAnalysisRecord semantic,
            string partId)
        {
            if (semantic?.parts == null)
                return null;

            for (int index = 0;
                 index < semantic.parts.Count;
                 index++)
            {
                SavicSemanticPartRecord part =
                    semantic.parts[index];

                if (part != null &&
                    string.Equals(
                        part.partId,
                        partId,
                        StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return null;
        }

        private static bool HasRelation(
            SavicSemanticPartAnalysisRecord semantic,
            string sourcePartId,
            string targetPartId,
            string relation)
        {
            if (semantic?.relations == null)
                return false;

            for (int index = 0;
                 index < semantic.relations.Count;
                 index++)
            {
                SavicPartRelationRecord candidate =
                    semantic.relations[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.sourcePartId,
                        sourcePartId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.targetPartId,
                        targetPartId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.relation,
                        relation,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static SavicManifest BuildClassificationManifest(
            SavicManifest source,
            SavicModelAnalysisRecord model)
        {
            return new SavicManifest
            {
                source =
                    new SavicSourceRecord
                    {
                        originalFileName =
                            source.source.originalFileName,
                        extension =
                            source.source.extension,
                        sourceKind =
                            source.source.sourceKind
                    },
                model3D =
                    model
            };
        }

        private static SavicManifest FindPublishedTable(
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

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(
                    message);
        }
    }
}
