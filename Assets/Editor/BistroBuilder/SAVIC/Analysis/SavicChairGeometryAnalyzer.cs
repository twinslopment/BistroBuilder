using System;
using System.Globalization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicChairGeometryAnalyzer
    {
        internal const string Version = "2.0.0";

        private const int VerticalBinCount = 24;
        private const int MaximumSampledTrianglesPerMeshInstance = 200000;
        private const float MinimumTriangleArea = 0.00000001f;
        private const float SeatUpwardNormalThreshold = 0.58f;
        private const float VerticalNormalThreshold = 0.35f;
        private const float MinimumSeatCandidateHeight01 = 0.30f;
        private const float MaximumSeatCandidateHeight01 = 0.70f;

        internal static SavicChairGeometryProfileRecord Analyze(
            GameObject root,
            SavicModelAnalysisRecord model)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (model == null ||
                !model.hasUsableBounds)
            {
                return new SavicChairGeometryProfileRecord
                {
                    analyzed = false,
                    analyzerVersion = Version,
                    evidence =
                        "Chair geometry profile requires usable model bounds."
                };
            }

            ChairAccumulator accumulator =
                new ChairAccumulator(model);

            MeshFilter[] filters =
                root.GetComponentsInChildren<MeshFilter>(true);

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
            ChairAccumulator accumulator)
        {
            using Mesh.MeshDataArray dataArray =
                MeshUtility.AcquireReadOnlyMeshData(mesh);

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

            long sourceTriangleCount = 0;

            for (int subMeshIndex = 0;
                 subMeshIndex < data.subMeshCount;
                 subMeshIndex++)
            {
                SubMeshDescriptor descriptor =
                    data.GetSubMesh(subMeshIndex);

                if (descriptor.topology != MeshTopology.Triangles)
                    continue;

                sourceTriangleCount +=
                    Math.Max(
                        0,
                        descriptor.indexCount / 3);
            }

            if (sourceTriangleCount <= 0)
                return;

            int samplingStride =
                (int)Math.Max(
                    1L,
                    (sourceTriangleCount +
                     MaximumSampledTrianglesPerMeshInstance -
                     1L) /
                    MaximumSampledTrianglesPerMeshInstance);

            accumulator.RecordMeshInstance(
                sourceTriangleCount,
                samplingStride);

            for (int subMeshIndex = 0;
                 subMeshIndex < data.subMeshCount;
                 subMeshIndex++)
            {
                SubMeshDescriptor descriptor =
                    data.GetSubMesh(subMeshIndex);

                if (descriptor.topology != MeshTopology.Triangles ||
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

                    int indexA = indices[offset];
                    int indexB = indices[offset + 1];
                    int indexC = indices[offset + 2];

                    if ((uint)indexA >= (uint)vertices.Length ||
                        (uint)indexB >= (uint)vertices.Length ||
                        (uint)indexC >= (uint)vertices.Length)
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
                        cross /
                        doubleArea;

                    Vector3 centroid =
                        (a + b + c) /
                        3f;

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
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private sealed class ChairAccumulator
        {
            private readonly float minimumX;
            private readonly float minimumY;
            private readonly float minimumZ;
            private readonly float width;
            private readonly float height;
            private readonly float depth;
            private readonly double footprintArea;

            private readonly double[] totalAreaByBin =
                new double[VerticalBinCount];

            private readonly double[] upwardAreaByBin =
                new double[VerticalBinCount];

            private readonly double[] upwardProjectedByBin =
                new double[VerticalBinCount];

            private readonly double[] upwardHeightByBin =
                new double[VerticalBinCount];

            private readonly double[] verticalAreaByBin =
                new double[VerticalBinCount];

            private readonly double[] verticalXByBin =
                new double[VerticalBinCount];

            private readonly double[] verticalZByBin =
                new double[VerticalBinCount];

            private long sourceTriangleCount;
            private long sampledTriangleCount;
            private long invalidTriangleCount;
            private long degenerateTriangleCount;
            private int meshInstanceCount;
            private int maximumSamplingStride = 1;
            private double totalArea;

            internal ChairAccumulator(
                SavicModelAnalysisRecord model)
            {
                minimumX =
                    model.boundsCenterX -
                    model.widthMeters * 0.5f;

                minimumY =
                    model.boundsCenterY -
                    model.heightMeters * 0.5f;

                minimumZ =
                    model.boundsCenterZ -
                    model.depthMeters * 0.5f;

                width =
                    Math.Max(
                        0.0001f,
                        model.widthMeters);

                height =
                    Math.Max(
                        0.0001f,
                        model.heightMeters);

                depth =
                    Math.Max(
                        0.0001f,
                        model.depthMeters);

                footprintArea =
                    Math.Max(
                        0.000001d,
                        (double)width *
                        depth);
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

                int bin =
                    Math.Min(
                        VerticalBinCount - 1,
                        Math.Max(
                            0,
                            (int)(height01 *
                                  VerticalBinCount)));

                totalArea +=
                    weightedArea;

                totalAreaByBin[bin] +=
                    weightedArea;

                if (normal.y >=
                    SeatUpwardNormalThreshold)
                {
                    upwardAreaByBin[bin] +=
                        weightedArea;

                    upwardProjectedByBin[bin] +=
                        weightedArea *
                        normal.y;

                    upwardHeightByBin[bin] +=
                        weightedArea *
                        height01;
                }

                if (Mathf.Abs(normal.y) <=
                    VerticalNormalThreshold)
                {
                    float x01 =
                        Mathf.Clamp01(
                            (centroid.x -
                             minimumX) /
                            width);

                    float z01 =
                        Mathf.Clamp01(
                            (centroid.z -
                             minimumZ) /
                            depth);

                    verticalAreaByBin[bin] +=
                        weightedArea;

                    verticalXByBin[bin] +=
                        weightedArea *
                        x01;

                    verticalZByBin[bin] +=
                        weightedArea *
                        z01;
                }
            }

            internal SavicChairGeometryProfileRecord Build()
            {
                bool hasSurface =
                    sampledTriangleCount > 0 &&
                    totalArea >
                        MinimumTriangleArea;

                if (!hasSurface)
                {
                    return new SavicChairGeometryProfileRecord
                    {
                        analyzed = true,
                        analyzerVersion = Version,
                        usable = false,
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

                int firstSeatBin =
                    Mathf.Clamp(
                        Mathf.FloorToInt(
                            MinimumSeatCandidateHeight01 *
                            VerticalBinCount),
                        0,
                        VerticalBinCount - 1);

                int lastSeatBin =
                    Mathf.Clamp(
                        Mathf.CeilToInt(
                            MaximumSeatCandidateHeight01 *
                            VerticalBinCount),
                        0,
                        VerticalBinCount - 1);

                int seatBin =
                    -1;

                double bestSeatProjected =
                    0d;

                for (int bin = firstSeatBin;
                     bin <= lastSeatBin;
                     bin++)
                {
                    double candidate =
                        SumNeighborhood(
                            upwardProjectedByBin,
                            bin,
                            1);

                    if (candidate >
                        bestSeatProjected)
                    {
                        bestSeatProjected =
                            candidate;
                        seatBin =
                            bin;
                    }
                }

                if (seatBin < 0 ||
                    bestSeatProjected <= 0d)
                {
                    return new SavicChairGeometryProfileRecord
                    {
                        analyzed = true,
                        analyzerVersion = Version,
                        usable = false,
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
                            "No upward-facing seat candidate was found in the usable chair height band."
                    };
                }

                double seatUpwardArea =
                    SumNeighborhood(
                        upwardAreaByBin,
                        seatBin,
                        1);

                double seatHeightWeighted =
                    SumNeighborhood(
                        upwardHeightByBin,
                        seatBin,
                        1);

                float seatHeight01 =
                    seatUpwardArea > 0d
                        ? (float)Math.Clamp(
                            seatHeightWeighted /
                            seatUpwardArea,
                            0d,
                            1d)
                        : ((float)seatBin + 0.5f) /
                          VerticalBinCount;

                float seatProjectedCoverage =
                    (float)Math.Clamp(
                        bestSeatProjected /
                        footprintArea,
                        0d,
                        2d);

                float seatUpwardAreaRatio =
                    Ratio(
                        seatUpwardArea,
                        totalArea);

                float upperThreshold =
                    Mathf.Clamp01(
                        seatHeight01 +
                        0.07f);

                int firstBackBin =
                    Mathf.Clamp(
                        Mathf.FloorToInt(
                            upperThreshold *
                            VerticalBinCount),
                        0,
                        VerticalBinCount - 1);

                double upperVerticalArea = 0d;
                double upperVerticalX = 0d;
                double upperVerticalZ = 0d;

                for (int bin = firstBackBin;
                     bin < VerticalBinCount;
                     bin++)
                {
                    upperVerticalArea +=
                        verticalAreaByBin[bin];

                    upperVerticalX +=
                        verticalXByBin[bin];

                    upperVerticalZ +=
                        verticalZByBin[bin];
                }

                float upperVerticalAreaRatio =
                    Ratio(
                        upperVerticalArea,
                        totalArea);

                float upperVerticalCentroidX01 =
                    upperVerticalArea > 0d
                        ? (float)Math.Clamp(
                            upperVerticalX /
                            upperVerticalArea,
                            0d,
                            1d)
                        : 0.5f;

                float upperVerticalCentroidZ01 =
                    upperVerticalArea > 0d
                        ? (float)Math.Clamp(
                            upperVerticalZ /
                            upperVerticalArea,
                            0d,
                            1d)
                        : 0.5f;

                float xBias =
                    Mathf.Abs(
                        upperVerticalCentroidX01 -
                        0.5f) *
                    2f;

                float zBias =
                    Mathf.Abs(
                        upperVerticalCentroidZ01 -
                        0.5f) *
                    2f;

                string backAxis =
                    xBias >= zBias
                        ? "X"
                        : "Z";

                float signedBias =
                    string.Equals(
                        backAxis,
                        "X",
                        StringComparison.Ordinal)
                        ? upperVerticalCentroidX01 -
                          0.5f
                        : upperVerticalCentroidZ01 -
                          0.5f;

                string backSide =
                    signedBias >= 0f
                        ? "POSITIVE"
                        : "NEGATIVE";

                float backEdgeBias =
                    Math.Max(
                        xBias,
                        zBias);

                float frontX = 0f;
                float frontZ = 0f;

                if (string.Equals(
                        backAxis,
                        "X",
                        StringComparison.Ordinal))
                {
                    frontX =
                        signedBias >= 0f
                            ? -1f
                            : 1f;
                }
                else
                {
                    frontZ =
                        signedBias >= 0f
                            ? -1f
                            : 1f;
                }

                int supportLastBin =
                    Mathf.Clamp(
                        Mathf.FloorToInt(
                            Math.Max(
                                0f,
                                seatHeight01 -
                                0.06f) *
                            VerticalBinCount),
                        0,
                        VerticalBinCount - 1);

                double lowerSupportArea = 0d;

                for (int bin = 0;
                     bin <= supportLastBin;
                     bin++)
                {
                    lowerSupportArea +=
                        totalAreaByBin[bin];
                }

                float lowerSupportAreaRatio =
                    Ratio(
                        lowerSupportArea,
                        totalArea);

                float confidence =
                    BuildConfidence(
                        seatHeight01,
                        seatProjectedCoverage,
                        seatUpwardAreaRatio,
                        upperVerticalAreaRatio,
                        backEdgeBias,
                        lowerSupportAreaRatio);

                bool usable =
                    seatHeight01 >= 0.32f &&
                    seatHeight01 <= 0.68f &&
                    seatProjectedCoverage >= 0.12f &&
                    seatUpwardAreaRatio >= 0.025f &&
                    upperVerticalAreaRatio >= 0.08f &&
                    lowerSupportAreaRatio >= 0.12f;

                return new SavicChairGeometryProfileRecord
                {
                    analyzed = true,
                    analyzerVersion = Version,
                    usable = usable,
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
                    seatHeight01 =
                        seatHeight01,
                    seatHeightMeters =
                        seatHeight01 *
                        height,
                    seatUpwardAreaRatio =
                        seatUpwardAreaRatio,
                    seatProjectedCoverage =
                        seatProjectedCoverage,
                    upperVerticalAreaRatio =
                        upperVerticalAreaRatio,
                    upperVerticalCentroidX01 =
                        upperVerticalCentroidX01,
                    upperVerticalCentroidZ01 =
                        upperVerticalCentroidZ01,
                    backAxis =
                        upperVerticalArea > 0d
                            ? backAxis
                            : "UNKNOWN",
                    backSide =
                        upperVerticalArea > 0d
                            ? backSide
                            : "UNKNOWN",
                    backEdgeBias =
                        backEdgeBias,
                    frontDirectionLocalX =
                        upperVerticalArea > 0d
                            ? frontX
                            : 0f,
                    frontDirectionLocalZ =
                        upperVerticalArea > 0d
                            ? frontZ
                            : 0f,
                    lowerSupportAreaRatio =
                        lowerSupportAreaRatio,
                    confidenceScore =
                        confidence,
                    evidence =
                        BuildEvidence(
                            seatHeight01,
                            seatProjectedCoverage,
                            seatUpwardAreaRatio,
                            upperVerticalAreaRatio,
                            backAxis,
                            backSide,
                            backEdgeBias,
                            lowerSupportAreaRatio,
                            confidence)
                };
            }

            private static double SumNeighborhood(
                double[] values,
                int center,
                int radius)
            {
                double result = 0d;

                int first =
                    Math.Max(
                        0,
                        center - radius);

                int last =
                    Math.Min(
                        values.Length - 1,
                        center + radius);

                for (int index = first;
                     index <= last;
                     index++)
                {
                    result +=
                        values[index];
                }

                return result;
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

            private static float BuildConfidence(
                float seatHeight01,
                float seatCoverage,
                float seatAreaRatio,
                float upperVerticalAreaRatio,
                float backEdgeBias,
                float lowerSupportAreaRatio)
            {
                float score = 0f;

                if (seatHeight01 >= 0.40f &&
                    seatHeight01 <= 0.60f)
                {
                    score += 0.22f;
                }
                else if (seatHeight01 >= 0.32f &&
                         seatHeight01 <= 0.68f)
                {
                    score += 0.12f;
                }

                score +=
                    Mathf.Clamp01(
                        seatCoverage /
                        0.55f) *
                    0.24f;

                score +=
                    Mathf.Clamp01(
                        seatAreaRatio /
                        0.16f) *
                    0.12f;

                score +=
                    Mathf.Clamp01(
                        upperVerticalAreaRatio /
                        0.28f) *
                    0.20f;

                score +=
                    Mathf.Clamp01(
                        backEdgeBias /
                        0.45f) *
                    0.12f;

                score +=
                    Mathf.Clamp01(
                        lowerSupportAreaRatio /
                        0.35f) *
                    0.10f;

                return Mathf.Clamp01(score);
            }

            private static string BuildEvidence(
                float seatHeight01,
                float seatCoverage,
                float seatAreaRatio,
                float upperVerticalAreaRatio,
                string backAxis,
                string backSide,
                float backEdgeBias,
                float lowerSupportAreaRatio,
                float confidence)
            {
                return
                    "seat height " +
                    seatHeight01.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "; seat projected coverage " +
                    seatCoverage.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "; seat upward area " +
                    seatAreaRatio.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "; upper vertical area " +
                    upperVerticalAreaRatio.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "; inferred back " +
                    backAxis +
                    "/" +
                    backSide +
                    " bias " +
                    backEdgeBias.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "; lower support area " +
                    lowerSupportAreaRatio.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "; confidence " +
                    confidence.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    ".";
            }
        }
    }
}
