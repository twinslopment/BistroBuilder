using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicChairSemanticPartAnalyzer
    {
        internal const string Version = "1.0.0";

        private const float MinimumTriangleArea = 0.00000001f;
        private const int MaximumSampledTrianglesPerMeshInstance = 250000;
        private const float MinimumAutomationConfidence = 0.68f;
        private const float GroundBand01 = 0.075f;

        internal static SavicSemanticPartAnalysisRecord Analyze(
            GameObject root,
            SavicModelAnalysisRecord model,
            SavicClassificationRecord classification)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            SavicSemanticPartAnalysisRecord result =
                new SavicSemanticPartAnalysisRecord
                {
                    analyzed = false,
                    analyzerVersion =
                        "chair-" + Version,
                    analyzedUtc =
                        DateTime.UtcNow.ToString("O")
                };

            if (model == null ||
                !model.hasUsableBounds ||
                model.chairGeometry == null ||
                !model.chairGeometry.analyzed ||
                !model.chairGeometry.usable)
            {
                result.evidence =
                    "Chair semantic analysis requires a usable chair geometry profile.";
                return result;
            }

            if (classification == null ||
                !string.Equals(
                    classification.type,
                    "Chair",
                    StringComparison.Ordinal))
            {
                result.evidence =
                    "Chair semantic analysis requires Chair classification.";
                return result;
            }

            ChairSemanticAccumulator accumulator =
                new ChairSemanticAccumulator(
                    model);

            AnalyzeMeshFilters(
                root,
                accumulator);

            AnalyzeSkinnedMeshes(
                root,
                accumulator);

            if (accumulator.ValidTriangleCount <= 0 ||
                accumulator.TotalArea <= 0d)
            {
                result.evidence =
                    "No non-degenerate triangle surface was available for chair semantic analysis.";
                return result;
            }

            SavicSupportPatternRecord supportPattern =
                accumulator.BuildSupportPattern();

            SavicSemanticPartRecord seat =
                accumulator.BuildPart(
                    accumulator.Seat,
                    "chair.seat",
                    "Seat",
                    BuildSeatConfidence(
                        model,
                        accumulator));

            SavicSemanticPartRecord back =
                accumulator.BuildPart(
                    accumulator.Back,
                    "chair.back",
                    "Backrest",
                    BuildBackConfidence(
                        model,
                        accumulator));

            string supportRole =
                string.Equals(
                    supportPattern.mode,
                    "MULTI_CONTACT",
                    StringComparison.Ordinal)
                    ? "LegSet"
                    : "BaseSupport";

            SavicSemanticPartRecord support =
                accumulator.BuildPart(
                    accumulator.Support,
                    "chair.support",
                    supportRole,
                    BuildSupportConfidence(
                        model,
                        accumulator,
                        supportPattern));

            SavicSemanticPartRecord arms =
                BuildArmsPart(
                    model,
                    accumulator);

            result.analyzed = true;
            result.rawRegionCount =
                accumulator.SourceMeshKeys.Count;
            result.regionDetailMode =
                "SOURCE_MESH_INSTANCES";
            result.rawRegionDetailTruncated =
                false;
            result.supportPattern =
                supportPattern;

            if (seat != null)
                result.parts.Add(seat);

            if (back != null)
                result.parts.Add(back);

            if (support != null)
                result.parts.Add(support);

            if (arms != null)
                result.parts.Add(arms);

            result.semanticPartCount =
                result.parts.Count;

            float coveredArea = 0f;

            for (int index = 0;
                 index < result.parts.Count;
                 index++)
            {
                coveredArea +=
                    Math.Max(
                        0f,
                        result.parts[index].areaFraction);
            }

            result.semanticCoverage =
                Mathf.Clamp01(
                    coveredArea);

            result.unresolvedAreaRatio =
                Mathf.Clamp01(
                    1f -
                    result.semanticCoverage);

            AddRelations(
                result,
                seat,
                back,
                support,
                arms);

            result.automationReady =
                seat != null &&
                back != null &&
                support != null &&
                seat.confidenceScore >=
                    MinimumAutomationConfidence &&
                back.confidenceScore >=
                    MinimumAutomationConfidence &&
                support.confidenceScore >=
                    MinimumAutomationConfidence &&
                result.semanticCoverage >= 0.72f &&
                model.chairGeometry.confidenceScore >= 0.78f &&
                !string.Equals(
                    model.chairGeometry.backAxis,
                    "UNKNOWN",
                    StringComparison.Ordinal);

            result.evidence =
                BuildEvidence(
                    model,
                    result,
                    seat,
                    back,
                    support,
                    arms);

            return result;
        }

        private static void AnalyzeMeshFilters(
            GameObject root,
            ChairSemanticAccumulator accumulator)
        {
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
                    root.transform.worldToLocalMatrix *
                    filter.transform.localToWorldMatrix,
                    BuildHierarchyKey(
                        root.transform,
                        filter.transform),
                    accumulator);
            }
        }

        private static void AnalyzeSkinnedMeshes(
            GameObject root,
            ChairSemanticAccumulator accumulator)
        {
            SkinnedMeshRenderer[] renderers =
                root.GetComponentsInChildren
                    <SkinnedMeshRenderer>(true);

            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                SkinnedMeshRenderer renderer =
                    renderers[index];

                if (renderer == null ||
                    renderer.sharedMesh == null)
                {
                    continue;
                }

                AnalyzeMeshInstance(
                    renderer.sharedMesh,
                    root.transform.worldToLocalMatrix *
                    renderer.transform.localToWorldMatrix,
                    BuildHierarchyKey(
                        root.transform,
                        renderer.transform),
                    accumulator);
            }
        }

        private static void AnalyzeMeshInstance(
            Mesh mesh,
            Matrix4x4 toRoot,
            string instanceKey,
            ChairSemanticAccumulator accumulator)
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

            accumulator.SourceMeshKeys.Add(
                instanceKey);

            using NativeArray<Vector3> vertices =
                new NativeArray<Vector3>(
                    data.vertexCount,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);

            data.GetVertices(vertices);

            long meshTriangleCount = 0;

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
                        descriptor.indexCount /
                        3);
            }

            if (meshTriangleCount <= 0)
                return;

            int stride =
                (int)Math.Max(
                    1L,
                    (meshTriangleCount +
                     MaximumSampledTrianglesPerMeshInstance -
                     1L) /
                    MaximumSampledTrianglesPerMeshInstance);

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
                    descriptor.indexCount /
                    3;

                int sampledCount =
                    (triangleCount +
                     stride -
                     1) /
                    stride;

                double sampleWeight =
                    sampledCount > 0
                        ? (double)triangleCount /
                          sampledCount
                        : 1d;

                for (int triangleIndex = 0;
                     triangleIndex < triangleCount;
                     triangleIndex += stride)
                {
                    int offset =
                        triangleIndex *
                        3;

                    int ia =
                        indices[offset];

                    int ib =
                        indices[offset + 1];

                    int ic =
                        indices[offset + 2];

                    if ((uint)ia >=
                            (uint)vertices.Length ||
                        (uint)ib >=
                            (uint)vertices.Length ||
                        (uint)ic >=
                            (uint)vertices.Length)
                    {
                        continue;
                    }

                    Vector3 a =
                        toRoot.MultiplyPoint3x4(
                            vertices[ia]);

                    Vector3 b =
                        toRoot.MultiplyPoint3x4(
                            vertices[ib]);

                    Vector3 c =
                        toRoot.MultiplyPoint3x4(
                            vertices[ic]);

                    if (!IsFinite(a) ||
                        !IsFinite(b) ||
                        !IsFinite(c))
                    {
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
                            MinimumTriangleArea *
                            2f)
                    {
                        continue;
                    }

                    float area =
                        doubleArea *
                        0.5f;

                    Vector3 normal =
                        cross /
                        doubleArea;

                    Vector3 centroid =
                        (a + b + c) /
                        3f;

                    accumulator.RecordTriangle(
                        a,
                        b,
                        c,
                        centroid,
                        normal,
                        area *
                        sampleWeight,
                        instanceKey);
                }
            }
        }

        private static float BuildSeatConfidence(
            SavicModelAnalysisRecord model,
            ChairSemanticAccumulator accumulator)
        {
            SavicChairGeometryProfileRecord geometry =
                model.chairGeometry;

            float score =
                0.34f +
                Mathf.Clamp01(
                    geometry.seatProjectedCoverage /
                    0.70f) *
                0.24f +
                Mathf.Clamp01(
                    geometry.seatUpwardAreaRatio /
                    0.16f) *
                0.20f +
                Mathf.Clamp01(
                    (float)(accumulator.Seat.Area /
                            Math.Max(
                                MinimumTriangleArea,
                                accumulator.TotalArea)) /
                    0.18f) *
                0.18f;

            return Mathf.Clamp01(score);
        }

        private static float BuildBackConfidence(
            SavicModelAnalysisRecord model,
            ChairSemanticAccumulator accumulator)
        {
            SavicChairGeometryProfileRecord geometry =
                model.chairGeometry;

            float score =
                0.28f +
                Mathf.Clamp01(
                    geometry.upperVerticalAreaRatio /
                    0.28f) *
                0.28f +
                Mathf.Clamp01(
                    geometry.backEdgeBias /
                    0.55f) *
                0.28f +
                Mathf.Clamp01(
                    (float)(accumulator.Back.Area /
                            Math.Max(
                                MinimumTriangleArea,
                                accumulator.TotalArea)) /
                    0.28f) *
                0.16f;

            return Mathf.Clamp01(score);
        }

        private static float BuildSupportConfidence(
            SavicModelAnalysisRecord model,
            ChairSemanticAccumulator accumulator,
            SavicSupportPatternRecord pattern)
        {
            float supportAreaRatio =
                (float)(accumulator.Support.Area /
                        Math.Max(
                            MinimumTriangleArea,
                            accumulator.TotalArea));

            float score =
                0.34f +
                Mathf.Clamp01(
                    model.chairGeometry.lowerSupportAreaRatio /
                    0.40f) *
                0.30f +
                Mathf.Clamp01(
                    supportAreaRatio /
                    0.42f) *
                0.22f +
                Mathf.Clamp01(
                    pattern.confidenceScore) *
                0.14f;

            return Mathf.Clamp01(score);
        }

        private static SavicSemanticPartRecord BuildArmsPart(
            SavicModelAnalysisRecord model,
            ChairSemanticAccumulator accumulator)
        {
            double totalArea =
                Math.Max(
                    MinimumTriangleArea,
                    accumulator.TotalArea);

            float leftRatio =
                (float)(accumulator.ArmNegative.Area /
                        totalArea);

            float rightRatio =
                (float)(accumulator.ArmPositive.Area /
                        totalArea);

            float combinedRatio =
                leftRatio +
                rightRatio;

            bool bilateral =
                leftRatio >= 0.006f &&
                rightRatio >= 0.006f;

            if (!bilateral ||
                combinedRatio < 0.018f)
            {
                return null;
            }

            PartAccumulator combined =
                PartAccumulator.Combine(
                    accumulator.ArmNegative,
                    accumulator.ArmPositive);

            float balance =
                1f -
                Mathf.Clamp01(
                    Mathf.Abs(
                        leftRatio -
                        rightRatio) /
                    Math.Max(
                        0.001f,
                        combinedRatio));

            float confidence =
                Mathf.Clamp01(
                    0.54f +
                    balance *
                    0.24f +
                    Mathf.Clamp01(
                        combinedRatio /
                        0.12f) *
                    0.22f);

            return accumulator.BuildPart(
                combined,
                "chair.arms",
                "ArmSet",
                confidence);
        }

        private static void AddRelations(
            SavicSemanticPartAnalysisRecord result,
            SavicSemanticPartRecord seat,
            SavicSemanticPartRecord back,
            SavicSemanticPartRecord support,
            SavicSemanticPartRecord arms)
        {
            if (seat == null)
                return;

            if (support != null)
            {
                AddRelation(
                    result,
                    support,
                    seat,
                    "SUPPORTS",
                    "Lower chair structure supports the detected seat zone.");
            }

            if (back != null)
            {
                AddRelation(
                    result,
                    back,
                    seat,
                    "ATTACHED_TO",
                    "Upper edge-biased vertical structure is attached to the seat.");
            }

            if (arms != null)
            {
                AddRelation(
                    result,
                    arms,
                    seat,
                    "ATTACHED_TO",
                    "Bilateral side structures are attached around the seat.");
            }
        }

        private static void AddRelation(
            SavicSemanticPartAnalysisRecord result,
            SavicSemanticPartRecord source,
            SavicSemanticPartRecord target,
            string relation,
            string evidence)
        {
            result.relations.Add(
                new SavicPartRelationRecord
                {
                    sourcePartId =
                        source.partId,
                    targetPartId =
                        target.partId,
                    relation =
                        relation,
                    confidence =
                        Math.Min(
                            source.confidenceScore,
                            target.confidenceScore),
                    evidence =
                        evidence
                });
        }

        private static string BuildEvidence(
            SavicModelAnalysisRecord model,
            SavicSemanticPartAnalysisRecord result,
            SavicSemanticPartRecord seat,
            SavicSemanticPartRecord back,
            SavicSemanticPartRecord support,
            SavicSemanticPartRecord arms)
        {
            return
                "chair semantic zones " +
                result.semanticPartCount.ToString(
                    CultureInfo.InvariantCulture) +
                "; coverage " +
                result.semanticCoverage.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                "; seat " +
                ConfidenceLabel(seat) +
                "; back " +
                ConfidenceLabel(back) +
                "; support " +
                ConfidenceLabel(support) +
                "; arms " +
                ConfidenceLabel(arms) +
                "; front (" +
                model.chairGeometry.frontDirectionLocalX.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                "," +
                model.chairGeometry.frontDirectionLocalZ.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture) +
                "); support pattern " +
                (result.supportPattern?.mode ??
                 "UNASSESSED") +
                ".";
        }

        private static string ConfidenceLabel(
            SavicSemanticPartRecord part)
        {
            if (part == null)
                return "NONE";

            return
                part.confidence +
                " " +
                part.confidenceScore.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
        }

        private static string BuildHierarchyKey(
            Transform root,
            Transform current)
        {
            if (current == null)
                return string.Empty;

            List<string> names =
                new List<string>();

            Transform cursor =
                current;

            while (cursor != null)
            {
                names.Add(
                    cursor.name ?? string.Empty);

                if (ReferenceEquals(
                        cursor,
                        root))
                {
                    break;
                }

                cursor =
                    cursor.parent;
            }

            names.Reverse();

            return string.Join(
                "/",
                names);
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

        private sealed class ChairSemanticAccumulator
        {
            private readonly SavicModelAnalysisRecord model;
            private readonly SavicChairGeometryProfileRecord geometry;
            private readonly float minimumX;
            private readonly float minimumY;
            private readonly float minimumZ;
            private readonly float width;
            private readonly float height;
            private readonly float depth;

            private readonly GroundZoneAccumulator[] groundZones =
            {
                new GroundZoneAccumulator(0),
                new GroundZoneAccumulator(1),
                new GroundZoneAccumulator(2),
                new GroundZoneAccumulator(3)
            };

            internal ChairSemanticAccumulator(
                SavicModelAnalysisRecord model)
            {
                this.model =
                    model;

                geometry =
                    model.chairGeometry;

                minimumX =
                    model.boundsCenterX -
                    model.widthMeters *
                    0.5f;

                minimumY =
                    model.boundsCenterY -
                    model.heightMeters *
                    0.5f;

                minimumZ =
                    model.boundsCenterZ -
                    model.depthMeters *
                    0.5f;

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
            }

            internal PartAccumulator Seat { get; } =
                new PartAccumulator();

            internal PartAccumulator Back { get; } =
                new PartAccumulator();

            internal PartAccumulator Support { get; } =
                new PartAccumulator();

            internal PartAccumulator ArmNegative { get; } =
                new PartAccumulator();

            internal PartAccumulator ArmPositive { get; } =
                new PartAccumulator();

            internal HashSet<string> SourceMeshKeys { get; } =
                new HashSet<string>(
                    StringComparer.Ordinal);

            internal long ValidTriangleCount { get; private set; }

            internal double TotalArea { get; private set; }

            internal void RecordTriangle(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                Vector3 centroid,
                Vector3 normal,
                double weightedArea,
                string sourceKey)
            {
                if (weightedArea <= 0d ||
                    double.IsNaN(weightedArea) ||
                    double.IsInfinity(weightedArea))
                {
                    return;
                }

                ValidTriangleCount++;
                TotalArea +=
                    weightedArea;

                float x01 =
                    Mathf.Clamp01(
                        (centroid.x -
                         minimumX) /
                        width);

                float y01 =
                    Mathf.Clamp01(
                        (centroid.y -
                         minimumY) /
                        height);

                float z01 =
                    Mathf.Clamp01(
                        (centroid.z -
                         minimumZ) /
                        depth);

                float seatY =
                    geometry.seatHeight01;

                float backCoordinate =
                    string.Equals(
                        geometry.backAxis,
                        "X",
                        StringComparison.Ordinal)
                        ? x01
                        : z01;

                bool backPositive =
                    string.Equals(
                        geometry.backSide,
                        "POSITIVE",
                        StringComparison.Ordinal);

                bool nearBackEdge =
                    backPositive
                        ? backCoordinate >= 0.58f
                        : backCoordinate <= 0.42f;

                float lateralCoordinate =
                    string.Equals(
                        geometry.backAxis,
                        "X",
                        StringComparison.Ordinal)
                        ? z01
                        : x01;

                float lateralSigned =
                    lateralCoordinate -
                    0.5f;

                bool nearSide =
                    Mathf.Abs(
                        lateralSigned) >=
                    0.28f;

                bool upper =
                    y01 >=
                    seatY +
                    0.045f;

                bool lower =
                    y01 <=
                    seatY -
                    0.055f;

                bool seatBand =
                    y01 >=
                        seatY -
                        0.075f &&
                    y01 <=
                        seatY +
                        0.095f;

                bool armBand =
                    y01 >=
                        seatY +
                        0.055f &&
                    y01 <=
                        Math.Min(
                            0.86f,
                            seatY +
                            0.30f);

                float absoluteNormalY =
                    Mathf.Abs(
                        normal.y);

                PartAccumulator target =
                    null;

                if (upper &&
                    nearBackEdge &&
                    (absoluteNormalY <= 0.78f ||
                     y01 >= seatY + 0.14f))
                {
                    target =
                        Back;
                }
                else if (armBand &&
                         nearSide &&
                         !nearBackEdge)
                {
                    target =
                        lateralSigned < 0f
                            ? ArmNegative
                            : ArmPositive;
                }
                else if (seatBand)
                {
                    target =
                        Seat;
                }
                else if (lower)
                {
                    target =
                        Support;
                }
                else if (upper &&
                         nearBackEdge)
                {
                    target =
                        Back;
                }

                if (target != null)
                {
                    target.AddTriangle(
                        a,
                        b,
                        c,
                        normal,
                        weightedArea,
                        sourceKey);
                }

                if (y01 <=
                    GroundBand01)
                {
                    int quadrant =
                        (x01 >= 0.5f ? 1 : 0) +
                        (z01 >= 0.5f ? 2 : 0);

                    groundZones[quadrant]
                        .Add(
                            x01,
                            z01,
                            weightedArea);
                }
            }

            internal SavicSemanticPartRecord BuildPart(
                PartAccumulator source,
                string partId,
                string role,
                float confidence)
            {
                if (source == null ||
                    source.Area <=
                        MinimumTriangleArea ||
                    TotalArea <=
                        MinimumTriangleArea)
                {
                    return null;
                }

                float areaFraction =
                    (float)Math.Clamp(
                        source.Area /
                        TotalArea,
                        0d,
                        1d);

                if (areaFraction < 0.01f)
                    return null;

                return source.Build(
                    partId,
                    role,
                    confidence,
                    areaFraction,
                    model);
            }

            internal SavicSupportPatternRecord BuildSupportPattern()
            {
                SavicSupportPatternRecord result =
                    new SavicSupportPatternRecord
                    {
                        analyzed = true
                    };

                double groundArea = 0d;

                for (int index = 0;
                     index < groundZones.Length;
                     index++)
                {
                    groundArea +=
                        groundZones[index].Area;
                }

                double minimumZoneArea =
                    Math.Max(
                        MinimumTriangleArea,
                        TotalArea *
                        0.0008d);

                int activeZones = 0;

                float minCenterX = 1f;
                float maxCenterX = 0f;
                float minCenterZ = 1f;
                float maxCenterZ = 0f;

                for (int index = 0;
                     index < groundZones.Length;
                     index++)
                {
                    GroundZoneAccumulator zone =
                        groundZones[index];

                    if (zone.Area <
                        minimumZoneArea)
                    {
                        continue;
                    }

                    SavicSupportZoneRecord built =
                        zone.Build(
                            "chair.support." +
                            index.ToString(
                                "00",
                                CultureInfo.InvariantCulture),
                            groundArea);

                    result.zones.Add(
                        built);

                    activeZones++;

                    minCenterX =
                        Math.Min(
                            minCenterX,
                            built.normalizedCenterX);

                    maxCenterX =
                        Math.Max(
                            maxCenterX,
                            built.normalizedCenterX);

                    minCenterZ =
                        Math.Min(
                            minCenterZ,
                            built.normalizedCenterZ);

                    maxCenterZ =
                        Math.Max(
                            maxCenterZ,
                            built.normalizedCenterZ);
                }

                result.zoneCount =
                    activeZones;

                result.supportSpanXRatio =
                    activeZones > 1
                        ? Mathf.Clamp01(
                            maxCenterX -
                            minCenterX)
                        : 0f;

                result.supportSpanZRatio =
                    activeZones > 1
                        ? Mathf.Clamp01(
                            maxCenterZ -
                            minCenterZ)
                        : 0f;

                result.supportPolygonAreaRatio =
                    result.supportSpanXRatio *
                    result.supportSpanZRatio;

                result.centerSupported =
                    false;

                result.broadBaseAreaRatio =
                    TotalArea > 0d
                        ? (float)Math.Clamp(
                            groundArea /
                            TotalArea,
                            0d,
                            1d)
                        : 0f;

                if (activeZones >= 3)
                {
                    result.mode =
                        "MULTI_CONTACT";

                    result.confidence =
                        "HIGH";

                    result.confidenceScore =
                        Mathf.Clamp01(
                            0.72f +
                            Math.Min(
                                0.20f,
                                (activeZones - 3) *
                                0.08f) +
                            result.supportPolygonAreaRatio *
                            0.08f);
                }
                else if (activeZones == 1 &&
                         result.broadBaseAreaRatio >= 0.05f)
                {
                    result.mode =
                        "BROAD_BASE";

                    result.confidence =
                        "MEDIUM";

                    result.confidenceScore =
                        0.68f;
                }
                else
                {
                    result.mode =
                        "CENTRAL_OR_MIXED";

                    result.confidence =
                        activeZones > 0
                            ? "MEDIUM"
                            : "LOW";

                    result.confidenceScore =
                        activeZones > 0
                            ? 0.58f
                            : 0.30f;
                }

                result.evidence =
                    activeZones.ToString(
                        CultureInfo.InvariantCulture) +
                    " ground-contact zone(s); span " +
                    result.supportSpanXRatio.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "x" +
                    result.supportSpanZRatio.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    "; ground area ratio " +
                    result.broadBaseAreaRatio.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture) +
                    ".";

                return result;
            }
        }

        private sealed class PartAccumulator
        {
            private readonly HashSet<string> sourceKeys =
                new HashSet<string>(
                    StringComparer.Ordinal);

            private bool hasBounds;
            private Vector3 minimum;
            private Vector3 maximum;
            private double weightedAbsNormalX;
            private double weightedAbsNormalY;
            private double weightedAbsNormalZ;

            internal double Area { get; private set; }
            internal long TriangleCount { get; private set; }

            internal void AddTriangle(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                Vector3 normal,
                double area,
                string sourceKey)
            {
                if (area <= 0d)
                    return;

                Area +=
                    area;

                TriangleCount++;

                weightedAbsNormalX +=
                    Math.Abs(normal.x) *
                    area;

                weightedAbsNormalY +=
                    Math.Abs(normal.y) *
                    area;

                weightedAbsNormalZ +=
                    Math.Abs(normal.z) *
                    area;

                Include(a);
                Include(b);
                Include(c);

                if (!string.IsNullOrWhiteSpace(
                        sourceKey))
                {
                    sourceKeys.Add(
                        sourceKey);
                }
            }

            internal SavicSemanticPartRecord Build(
                string partId,
                string role,
                float confidence,
                float areaFraction,
                SavicModelAnalysisRecord model)
            {
                Vector3 size =
                    hasBounds
                        ? maximum -
                          minimum
                        : Vector3.zero;

                Vector3 center =
                    hasBounds
                        ? (minimum +
                           maximum) *
                          0.5f
                        : Vector3.zero;

                float minX =
                    model.boundsCenterX -
                    model.widthMeters *
                    0.5f;

                float minY =
                    model.boundsCenterY -
                    model.heightMeters *
                    0.5f;

                float minZ =
                    model.boundsCenterZ -
                    model.depthMeters *
                    0.5f;

                string[] keys =
                    new string[sourceKeys.Count];

                sourceKeys.CopyTo(
                    keys);

                Array.Sort(
                    keys,
                    StringComparer.Ordinal);

                return new SavicSemanticPartRecord
                {
                    partId =
                        partId,
                    role =
                        role,
                    confidence =
                        ConfidenceName(
                            confidence),
                    confidenceScore =
                        Mathf.Clamp01(
                            confidence),
                    syntheticZone =
                        true,
                    movableCandidate =
                        false,
                    sourceRegionCount =
                        sourceKeys.Count,
                    sourceRegionKeys =
                        string.Join(
                            ";",
                            keys),
                    triangleCount =
                        TriangleCount,
                    areaFraction =
                        areaFraction,
                    centerX =
                        center.x,
                    centerY =
                        center.y,
                    centerZ =
                        center.z,
                    sizeX =
                        size.x,
                    sizeY =
                        size.y,
                    sizeZ =
                        size.z,
                    normalizedCenterX =
                        Mathf.Clamp01(
                            (center.x -
                             minX) /
                            Math.Max(
                                0.0001f,
                                model.widthMeters)),
                    normalizedCenterY =
                        Mathf.Clamp01(
                            (center.y -
                             minY) /
                            Math.Max(
                                0.0001f,
                                model.heightMeters)),
                    normalizedCenterZ =
                        Mathf.Clamp01(
                            (center.z -
                             minZ) /
                            Math.Max(
                                0.0001f,
                                model.depthMeters)),
                    normalizedSizeX =
                        Mathf.Clamp01(
                            size.x /
                            Math.Max(
                                0.0001f,
                                model.widthMeters)),
                    normalizedSizeY =
                        Mathf.Clamp01(
                            size.y /
                            Math.Max(
                                0.0001f,
                                model.heightMeters)),
                    normalizedSizeZ =
                        Mathf.Clamp01(
                            size.z /
                            Math.Max(
                                0.0001f,
                                model.depthMeters)),
                    meanAbsoluteNormalX =
                        WeightedNormal(
                            weightedAbsNormalX),
                    meanAbsoluteNormalY =
                        WeightedNormal(
                            weightedAbsNormalY),
                    meanAbsoluteNormalZ =
                        WeightedNormal(
                            weightedAbsNormalZ),
                    evidence =
                        "Geometry-zone grouping merged " +
                        sourceKeys.Count.ToString(
                            CultureInfo.InvariantCulture) +
                        " source mesh instance(s); area fraction " +
                        areaFraction.ToString(
                            "0.###",
                            CultureInfo.InvariantCulture) +
                        "."
                };
            }

            internal static PartAccumulator Combine(
                PartAccumulator first,
                PartAccumulator second)
            {
                PartAccumulator result =
                    new PartAccumulator();

                result.MergeFrom(first);
                result.MergeFrom(second);

                return result;
            }

            private void MergeFrom(
                PartAccumulator source)
            {
                if (source == null ||
                    source.Area <= 0d)
                {
                    return;
                }

                Area +=
                    source.Area;

                TriangleCount +=
                    source.TriangleCount;

                weightedAbsNormalX +=
                    source.weightedAbsNormalX;

                weightedAbsNormalY +=
                    source.weightedAbsNormalY;

                weightedAbsNormalZ +=
                    source.weightedAbsNormalZ;

                if (source.hasBounds)
                {
                    Include(
                        source.minimum);

                    Include(
                        source.maximum);
                }

                foreach (string key in
                         source.sourceKeys)
                {
                    sourceKeys.Add(
                        key);
                }
            }

            private float WeightedNormal(
                double value)
            {
                return Area > 0d
                    ? (float)Math.Clamp(
                        value /
                        Area,
                        0d,
                        1d)
                    : 0f;
            }

            private void Include(
                Vector3 point)
            {
                if (!hasBounds)
                {
                    minimum =
                        point;

                    maximum =
                        point;

                    hasBounds =
                        true;

                    return;
                }

                minimum =
                    Vector3.Min(
                        minimum,
                        point);

                maximum =
                    Vector3.Max(
                        maximum,
                        point);
            }
        }

        private sealed class GroundZoneAccumulator
        {
            private readonly int index;
            private bool hasBounds;
            private float minX;
            private float maxX;
            private float minZ;
            private float maxZ;
            private double weightedX;
            private double weightedZ;

            internal GroundZoneAccumulator(
                int index)
            {
                this.index =
                    index;
            }

            internal double Area { get; private set; }
            internal int PointCount { get; private set; }

            internal void Add(
                float x01,
                float z01,
                double area)
            {
                if (area <= 0d)
                    return;

                Area +=
                    area;

                PointCount++;

                weightedX +=
                    x01 *
                    area;

                weightedZ +=
                    z01 *
                    area;

                if (!hasBounds)
                {
                    minX =
                        maxX =
                            x01;

                    minZ =
                        maxZ =
                            z01;

                    hasBounds =
                        true;
                }
                else
                {
                    minX =
                        Math.Min(
                            minX,
                            x01);

                    maxX =
                        Math.Max(
                            maxX,
                            x01);

                    minZ =
                        Math.Min(
                            minZ,
                            z01);

                    maxZ =
                        Math.Max(
                            maxZ,
                            z01);
                }
            }

            internal SavicSupportZoneRecord Build(
                string zoneId,
                double totalGroundArea)
            {
                float centerX =
                    Area > 0d
                        ? (float)(weightedX /
                                  Area)
                        : 0.5f;

                float centerZ =
                    Area > 0d
                        ? (float)(weightedZ /
                                  Area)
                        : 0.5f;

                float confidence =
                    totalGroundArea > 0d
                        ? Mathf.Clamp01(
                            0.55f +
                            (float)(Area /
                                    totalGroundArea) *
                            0.40f)
                        : 0.55f;

                return new SavicSupportZoneRecord
                {
                    zoneId =
                        zoneId,
                    pointCount =
                        PointCount,
                    normalizedCenterX =
                        centerX,
                    normalizedCenterZ =
                        centerZ,
                    normalizedSizeX =
                        hasBounds
                            ? Math.Max(
                                0.035f,
                                maxX -
                                minX)
                            : 0.035f,
                    normalizedSizeZ =
                        hasBounds
                            ? Math.Max(
                                0.035f,
                                maxZ -
                                minZ)
                            : 0.035f,
                    confidenceScore =
                        confidence,
                    evidence =
                        "Ground-contact quadrant " +
                        index.ToString(
                            CultureInfo.InvariantCulture) +
                        " with " +
                        PointCount.ToString(
                            CultureInfo.InvariantCulture) +
                        " sampled triangle(s)."
                };
            }
        }

        private static string ConfidenceName(
            float confidence)
        {
            if (confidence >= 0.82f)
                return "HIGH";

            if (confidence >= 0.62f)
                return "MEDIUM";

            if (confidence >= 0.42f)
                return "LOW";

            return "UNKNOWN";
        }
    }
}
