using System;
using System.Globalization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicGeometryProfileAnalyzer
    {
        internal const string Version = "1.1.0";

        private const int MaximumSampledTrianglesPerMeshInstance =
            250000;

        private const float MinimumTriangleArea =
            0.00000001f;

        private const float UpwardNormalThreshold =
            0.72f;

        private const float HorizontalNormalThreshold =
            0.78f;

        private const float VerticalNormalThreshold =
            0.30f;

        internal static SavicGeometryProfileRecord Analyze(
            GameObject root,
            SavicModelAnalysisRecord model)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (model == null ||
                !model.hasUsableBounds)
            {
                return new SavicGeometryProfileRecord
                {
                    analyzed = false,
                    analyzerVersion = Version,
                    evidence =
                        "Geometry profile requires usable model bounds."
                };
            }

            GeometryAccumulator accumulator =
                new GeometryAccumulator(
                    model);

            MeshFilter[] filters =
                root.GetComponentsInChildren
                    <MeshFilter>(true);

            for (int index = 0;
                 index < filters.Length;
                 index++)
            {
                MeshFilter filter =
                    filters[index];

                if (filter == null ||
                    filter.sharedMesh == null)
                {
                    continue;
                }

                AnalyzeMeshInstance(
                    filter.sharedMesh,
                    SavicMetricSpace.LocalToMetric(
                        root.transform,
                        filter.transform),
                    accumulator);
            }

            SkinnedMeshRenderer[] skinned =
                root.GetComponentsInChildren
                    <SkinnedMeshRenderer>(true);

            for (int index = 0;
                 index < skinned.Length;
                 index++)
            {
                SkinnedMeshRenderer renderer =
                    skinned[index];

                if (renderer == null ||
                    renderer.sharedMesh == null)
                {
                    continue;
                }

                AnalyzeMeshInstance(
                    renderer.sharedMesh,
                    SavicMetricSpace.LocalToMetric(
                        root.transform,
                        renderer.transform),
                    accumulator);
            }

            return accumulator.Build();
        }

        private static void AnalyzeMeshInstance(
            Mesh mesh,
            Matrix4x4 toRoot,
            GeometryAccumulator accumulator)
        {
            using Mesh.MeshDataArray dataArray =
                MeshUtility.AcquireReadOnlyMeshData(
                    mesh);

            if (dataArray.Length != 1)
            {
                throw new InvalidOperationException(
                    "Unity returned an unexpected MeshDataArray length.");
            }

            Mesh.MeshData data =
                dataArray[0];

            if (data.vertexCount <= 0)
                return;

            using NativeArray<Vector3> vertices =
                new NativeArray<Vector3>(
                    data.vertexCount,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);

            data.GetVertices(vertices);

            long meshTriangleCount =
                0;

            for (int subMeshIndex = 0;
                 subMeshIndex < data.subMeshCount;
                 subMeshIndex++)
            {
                SubMeshDescriptor descriptor =
                    data.GetSubMesh(
                        subMeshIndex);

                if (descriptor.topology !=
                    MeshTopology.Triangles)
                {
                    continue;
                }

                meshTriangleCount +=
                    Math.Max(
                        0,
                        descriptor.indexCount / 3);
            }

            if (meshTriangleCount <= 0)
                return;

            int samplingStride =
                (int)Math.Max(
                    1L,
                    (meshTriangleCount +
                     MaximumSampledTrianglesPerMeshInstance -
                     1L) /
                    MaximumSampledTrianglesPerMeshInstance);

            accumulator.RecordMeshInstance(
                meshTriangleCount,
                samplingStride);

            for (int subMeshIndex = 0;
                 subMeshIndex < data.subMeshCount;
                 subMeshIndex++)
            {
                SubMeshDescriptor descriptor =
                    data.GetSubMesh(
                        subMeshIndex);

                if (descriptor.topology !=
                        MeshTopology.Triangles ||
                    descriptor.indexCount < 3)
                {
                    continue;
                }

                using NativeArray<int> indices =
                    new NativeArray<int>(
                        descriptor.indexCount,
                        Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);

                data.GetIndices(
                    indices,
                    subMeshIndex,
                    true);

                int triangleCount =
                    descriptor.indexCount / 3;

                int sampledCount =
                    (triangleCount +
                     samplingStride -
                     1) /
                    samplingStride;

                double sampleWeight =
                    sampledCount > 0
                        ? (double)triangleCount /
                          sampledCount
                        : 1d;

                for (int triangleIndex = 0;
                     triangleIndex < triangleCount;
                     triangleIndex += samplingStride)
                {
                    int offset =
                        triangleIndex * 3;

                    int indexA =
                        indices[offset];
                    int indexB =
                        indices[offset + 1];
                    int indexC =
                        indices[offset + 2];

                    if ((uint)indexA >=
                            (uint)vertices.Length ||
                        (uint)indexB >=
                            (uint)vertices.Length ||
                        (uint)indexC >=
                            (uint)vertices.Length)
                    {
                        accumulator.RecordInvalidTriangle();
                        continue;
                    }

                    Vector3 a =
                        toRoot.MultiplyPoint3x4(
                            vertices[indexA]);

                    Vector3 b =
                        toRoot.MultiplyPoint3x4(
                            vertices[indexB]);

                    Vector3 c =
                        toRoot.MultiplyPoint3x4(
                            vertices[indexC]);

                    if (!IsFinite(a) ||
                        !IsFinite(b) ||
                        !IsFinite(c))
                    {
                        accumulator.RecordInvalidTriangle();
                        continue;
                    }

                    Vector3 cross =
                        Vector3.Cross(
                            b - a,
                            c - a);

                    float doubleArea =
                        cross.magnitude;

                    if (!IsFinite(doubleArea) ||
                        doubleArea <=
                            MinimumTriangleArea * 2f)
                    {
                        accumulator.RecordDegenerateTriangle();
                        continue;
                    }

                    float area =
                        doubleArea * 0.5f;

                    Vector3 normal =
                        cross / doubleArea;

                    Vector3 centroid =
                        (a + b + c) / 3f;

                    accumulator.RecordTriangle(
                        area,
                        normal,
                        centroid,
                        sampleWeight);
                }
            }
        }

        private static bool IsFinite(
            Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(
            float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private sealed class GeometryAccumulator
        {
            private readonly float minimumY;
            private readonly float height;
            private readonly double footprintArea;

            private long sourceTriangleCount;
            private long sampledTriangleCount;
            private long invalidTriangleCount;
            private long degenerateTriangleCount;
            private int meshInstanceCount;
            private int maximumSamplingStride = 1;

            private double totalArea;
            private double upwardArea;
            private double horizontalArea;
            private double verticalArea;
            private double upperBandArea;
            private double lowerBandArea;
            private double upperUpwardProjectedArea;
            private double lowerHorizontalProjectedArea;
            private double weightedHeight;

            internal GeometryAccumulator(
                SavicModelAnalysisRecord model)
            {
                minimumY =
                    model.boundsCenterY -
                    model.heightMeters * 0.5f;

                height =
                    Math.Max(
                        0.0001f,
                        model.heightMeters);

                footprintArea =
                    Math.Max(
                        0.000001d,
                        (double)model.widthMeters *
                        model.depthMeters);
            }

            internal void RecordMeshInstance(
                long triangles,
                int samplingStride)
            {
                meshInstanceCount++;

                sourceTriangleCount +=
                    Math.Max(
                        0L,
                        triangles);

                maximumSamplingStride =
                    Math.Max(
                        maximumSamplingStride,
                        samplingStride);
            }

            internal void RecordInvalidTriangle()
            {
                invalidTriangleCount++;
            }

            internal void RecordDegenerateTriangle()
            {
                degenerateTriangleCount++;
            }

            internal void RecordTriangle(
                float area,
                Vector3 normal,
                Vector3 centroid,
                double sampleWeight)
            {
                sampledTriangleCount++;

                double weightedArea =
                    area *
                    sampleWeight;

                if (weightedArea <= 0d ||
                    double.IsNaN(weightedArea) ||
                    double.IsInfinity(weightedArea))
                {
                    return;
                }

                float height01 =
                    Mathf.Clamp01(
                        (centroid.y -
                         minimumY) /
                        height);

                float normalY =
                    normal.y;

                float absoluteNormalY =
                    Mathf.Abs(
                        normalY);

                totalArea +=
                    weightedArea;

                weightedHeight +=
                    weightedArea *
                    height01;

                if (normalY >=
                    UpwardNormalThreshold)
                {
                    upwardArea +=
                        weightedArea;
                }

                if (absoluteNormalY >=
                    HorizontalNormalThreshold)
                {
                    horizontalArea +=
                        weightedArea;
                }

                if (absoluteNormalY <=
                    VerticalNormalThreshold)
                {
                    verticalArea +=
                        weightedArea;
                }

                if (height01 >= 0.60f)
                {
                    upperBandArea +=
                        weightedArea;
                }

                if (height01 <= 0.45f)
                {
                    lowerBandArea +=
                        weightedArea;
                }

                if (height01 >= 0.58f &&
                    normalY >=
                        UpwardNormalThreshold)
                {
                    upperUpwardProjectedArea +=
                        weightedArea *
                        normalY;
                }

                if (height01 <= 0.15f &&
                    absoluteNormalY >=
                        HorizontalNormalThreshold)
                {
                    lowerHorizontalProjectedArea +=
                        weightedArea *
                        absoluteNormalY;
                }
            }

            internal SavicGeometryProfileRecord Build()
            {
                bool usable =
                    sampledTriangleCount > 0 &&
                    totalArea >
                        MinimumTriangleArea;

                if (!usable)
                {
                    return new SavicGeometryProfileRecord
                    {
                        analyzed = true,
                        analyzerVersion = Version,
                        usable = false,
                        meshInstanceCount =
                            meshInstanceCount,
                        sourceTriangleCount =
                            sourceTriangleCount,
                        sampledTriangleCount =
                            sampledTriangleCount,
                        invalidTriangleCount =
                            invalidTriangleCount,
                        degenerateTriangleCount =
                            degenerateTriangleCount,
                        maximumSamplingStride =
                            maximumSamplingStride,
                        evidence =
                            "No non-degenerate triangle surface could be analyzed."
                    };
                }

                float upwardRatio =
                    Ratio(
                        upwardArea,
                        totalArea);

                float horizontalRatio =
                    Ratio(
                        horizontalArea,
                        totalArea);

                float verticalRatio =
                    Ratio(
                        verticalArea,
                        totalArea);

                float upperBandRatio =
                    Ratio(
                        upperBandArea,
                        totalArea);

                float lowerBandRatio =
                    Ratio(
                        lowerBandArea,
                        totalArea);

                float centroidHeight01 =
                    (float)Math.Clamp(
                        weightedHeight /
                        totalArea,
                        0d,
                        1d);

                float upperCoverage =
                    (float)Math.Clamp(
                        upperUpwardProjectedArea /
                        footprintArea,
                        0d,
                        2d);

                float lowerCoverage =
                    (float)Math.Clamp(
                        lowerHorizontalProjectedArea /
                        footprintArea,
                        0d,
                        2d);

                return new SavicGeometryProfileRecord
                {
                    analyzed = true,
                    analyzerVersion = Version,
                    usable = true,
                    meshInstanceCount =
                        meshInstanceCount,
                    sourceTriangleCount =
                        sourceTriangleCount,
                    sampledTriangleCount =
                        sampledTriangleCount,
                    invalidTriangleCount =
                        invalidTriangleCount,
                    degenerateTriangleCount =
                        degenerateTriangleCount,
                    maximumSamplingStride =
                        maximumSamplingStride,
                    estimatedSurfaceAreaSquareMeters =
                        (float)Math.Min(
                            totalArea,
                            float.MaxValue),
                    upwardFacingAreaRatio =
                        upwardRatio,
                    horizontalAreaRatio =
                        horizontalRatio,
                    verticalAreaRatio =
                        verticalRatio,
                    upperBandAreaRatio =
                        upperBandRatio,
                    lowerBandAreaRatio =
                        lowerBandRatio,
                    surfaceAreaCentroidHeight01 =
                        centroidHeight01,
                    upperUpwardProjectedCoverage =
                        upperCoverage,
                    lowerHorizontalProjectedCoverage =
                        lowerCoverage,
                    evidence =
                        BuildEvidence(
                            upwardRatio,
                            upperBandRatio,
                            lowerBandRatio,
                            centroidHeight01,
                            upperCoverage)
                };
            }

            private static float Ratio(
                double numerator,
                double denominator)
            {
                if (denominator <= 0d)
                    return 0f;

                return (float)Math.Clamp(
                    numerator /
                    denominator,
                    0d,
                    1d);
            }

            private static string BuildEvidence(
                float upwardRatio,
                float upperBandRatio,
                float lowerBandRatio,
                float centroidHeight01,
                float upperCoverage)
            {
                return
                    "upward area " +
                    upwardRatio.ToString("0.###", CultureInfo.InvariantCulture) +
                    "; upper-band area " +
                    upperBandRatio.ToString("0.###", CultureInfo.InvariantCulture) +
                    "; lower-band area " +
                    lowerBandRatio.ToString("0.###", CultureInfo.InvariantCulture) +
                    "; surface centroid height " +
                    centroidHeight01.ToString("0.###", CultureInfo.InvariantCulture) +
                    "; upper projected coverage " +
                    upperCoverage.ToString("0.###", CultureInfo.InvariantCulture) +
                    ".";
            }
        }
    }
}
