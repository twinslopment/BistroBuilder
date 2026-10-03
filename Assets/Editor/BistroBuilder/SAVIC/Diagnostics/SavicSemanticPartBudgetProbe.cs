using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicSemanticPartBudgetProbe
    {
        private const int OversizedVertexCount = 400001;

        public static void RunFromCommandLine()
        {
            RunOrThrow();
        }

        private static void RunOrThrow()
        {
            GameObject root =
                new GameObject(
                    "SemanticBudgetProbe");

            Mesh mesh =
                null;

            try
            {
                mesh =
                    BuildOversizedSparseMesh();

                MeshFilter filter =
                    root.AddComponent<MeshFilter>();

                filter.sharedMesh =
                    mesh;

                SavicModelAnalysisRecord model =
                    SavicModelAnalyzer.Analyze(
                        root);

                Require(
                    model.hasUsableBounds,
                    "Oversized diagnostic mesh has no usable bounds.");

                SavicClassificationRecord forcedTable =
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
                            "Semantic topology-budget diagnostic."
                    };

                SavicSemanticPartAnalysisRecord semantic =
                    SavicSemanticPartAnalyzer.Analyze(
                        root,
                        model,
                        forcedTable);

                Require(
                    semantic.analyzed,
                    "Semantic analyzer did not complete on oversized topology.");

                Require(
                    semantic.rawRegionDetailTruncated,
                    "Oversized topology did not activate bounded coarse mode.");

                Require(
                    string.Equals(
                        semantic.regionDetailMode,
                        "BOUNDED_COARSE",
                        StringComparison.Ordinal),
                    "Unexpected semantic topology detail mode.");

                Require(
                    semantic.rawRegionCount <= 1,
                    "Bounded coarse mode retained too many raw regions.");

                Debug.Log(
                    "[SAVIC] SEMANTIC PART BUDGET PROBE - PASS\n" +
                    "Vertices: " +
                    mesh.vertexCount +
                    "\nDetail mode: " +
                    semantic.regionDetailMode +
                    "\nRaw regions retained: " +
                    semantic.rawRegionCount);
            }
            finally
            {
                if (mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        mesh);
                }

                UnityEngine.Object.DestroyImmediate(
                    root);
            }
        }

        private static Mesh BuildOversizedSparseMesh()
        {
            Vector3[] vertices =
                new Vector3[
                    OversizedVertexCount];

            vertices[0] =
                new Vector3(-0.7f, 0f, -0.4f);
            vertices[1] =
                new Vector3(0.7f, 0f, -0.4f);
            vertices[2] =
                new Vector3(0.7f, 0f, 0.4f);
            vertices[3] =
                new Vector3(-0.7f, 0f, 0.4f);
            vertices[4] =
                new Vector3(-0.7f, 0.75f, -0.4f);
            vertices[5] =
                new Vector3(0.7f, 0.75f, -0.4f);
            vertices[6] =
                new Vector3(0.7f, 0.75f, 0.4f);
            vertices[7] =
                new Vector3(-0.7f, 0.75f, 0.4f);

            for (int index = 8;
                 index < vertices.Length;
                 index++)
            {
                vertices[index] =
                    Vector3.zero;
            }

            int[] triangles =
            {
                0, 2, 1,
                0, 3, 2,
                4, 5, 6,
                4, 6, 7,
                0, 1, 5,
                0, 5, 4,
                1, 2, 6,
                1, 6, 5,
                2, 3, 7,
                2, 7, 6,
                3, 0, 4,
                3, 4, 7
            };

            Mesh mesh =
                new Mesh
                {
                    name =
                        "SAVIC_SemanticBudgetProbe",
                    indexFormat =
                        IndexFormat.UInt32
                };

            mesh.vertices =
                vertices;

            mesh.triangles =
                triangles;

            mesh.RecalculateBounds();

            return mesh;
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
