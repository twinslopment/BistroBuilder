using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicSemanticPartAnalyzer
    {
        internal const string Version = "3.0.0";

        private const float MinimumTriangleArea = 0.00000001f;
        private const float TableVerticalSplit01 = 0.62f;
        private const float MinimumAutomationConfidence = 0.62f;
        private const float MinimumTabletopHorizontalEvidence = 0.50f;
        private const float GroundBand01 = 0.045f;
        private const float MinimumRawRegionAreaFraction = 0.00025f;
        private const int MaximumDetailedTopologyVertices = 400000;
        private const long MaximumDetailedTopologyTriangles = 600000L;
        private const int MaximumStoredRawRegions = 2048;

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
                    analyzerVersion = Version,
                    analyzedUtc = DateTime.UtcNow.ToString("O")
                };

            if (model == null ||
                !model.hasUsableBounds)
            {
                result.evidence =
                    "Semantic part analysis requires usable model bounds.";
                return result;
            }

            if (classification != null &&
                string.Equals(
                    classification.type,
                    "Chair",
                    StringComparison.Ordinal))
            {
                return SavicChairSemanticPartAnalyzer.Analyze(
                    root,
                    model,
                    classification);
            }

            if (classification == null ||
                !string.Equals(
                    classification.type,
                    "Table",
                    StringComparison.Ordinal))
            {
                result.evidence =
                    "Semantic part analysis supports Table and Chair content in V1.";
                return result;
            }

            TableSurfaceAccumulator accumulator =
                new TableSurfaceAccumulator(model);

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
                    "No non-degenerate triangle surface was available for semantic part analysis.";
                return result;
            }

            accumulator.FinalizeRawRegions();

            SavicSupportPatternRecord supportPattern =
                AnalyzeSupportPattern(
                    accumulator,
                    model);

            SavicSemanticPartRecord tabletop =
                BuildTabletopPart(
                    accumulator,
                    model);

            SavicSemanticPartRecord support =
                BuildSupportPart(
                    accumulator,
                    model,
                    supportPattern);

            result.analyzed = true;
            result.rawRegionCount =
                accumulator.RawRegions.Count;
            result.regionDetailMode =
                accumulator.RawRegionDetailTruncated
                    ? "BOUNDED_COARSE"
                    : "FULL";
            result.rawRegionDetailTruncated =
                accumulator.RawRegionDetailTruncated;
            result.supportPattern =
                supportPattern;

            if (tabletop != null)
                result.parts.Add(tabletop);

            if (support != null)
                result.parts.Add(support);

            result.semanticPartCount =
                result.parts.Count;

            float coveredArea =
                0f;

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

            if (tabletop != null &&
                support != null)
            {
                float relationConfidence =
                    Math.Min(
                        tabletop.confidenceScore,
                        support.confidenceScore);

                result.relations.Add(
                    new SavicPartRelationRecord
                    {
                        sourcePartId = support.partId,
                        targetPartId = tabletop.partId,
                        relation = "SUPPORTS",
                        confidence = relationConfidence,
                        evidence =
                            "Lower structural zone reaches the support plane beneath the upper work surface."
                    });

                result.relations.Add(
                    new SavicPartRelationRecord
                    {
                        sourcePartId = tabletop.partId,
                        targetPartId = support.partId,
                        relation = "SUPPORTED_BY",
                        confidence = relationConfidence,
                        evidence =
                            "Upper work surface is spatially carried by the lower structural zone."
                    });
            }

            result.automationReady =
                tabletop != null &&
                support != null &&
                tabletop.confidenceScore >=
                    MinimumAutomationConfidence &&
                tabletop.meanAbsoluteNormalY >=
                    MinimumTabletopHorizontalEvidence &&
                support.confidenceScore >=
                    MinimumAutomationConfidence &&
                result.semanticCoverage >= 0.95f &&
                supportPattern.analyzed &&
                supportPattern.confidenceScore >= 0.38f;

            result.evidence =
                BuildAnalysisEvidence(
                    result,
                    tabletop,
                    support,
                    supportPattern);

            return result;
        }

        private static void AnalyzeMeshFilters(
            GameObject root,
            TableSurfaceAccumulator accumulator)
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
                    SavicMetricSpace.LocalToMetric(
                        root.transform,
                        filter.transform),
                    BuildHierarchyKey(
                        root.transform,
                        filter.transform),
                    accumulator);
            }
        }

        private static void AnalyzeSkinnedMeshes(
            GameObject root,
            TableSurfaceAccumulator accumulator)
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
                    SavicMetricSpace.LocalToMetric(
                        root.transform,
                        renderer.transform),
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
            TableSurfaceAccumulator accumulator)
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

            using NativeArray<Vector3> sourceVertices =
                new NativeArray<Vector3>(
                    data.vertexCount,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);

            data.GetVertices(sourceVertices);

            Vector3[] vertices =
                new Vector3[data.vertexCount];

            for (int index = 0;
                 index < vertices.Length;
                 index++)
            {
                vertices[index] =
                    toRoot.MultiplyPoint3x4(
                        sourceVertices[index]);
            }

            long sourceTriangleCount =
                CountTriangleTopology(
                    data);

            bool detailedTopology =
                vertices.Length <=
                    MaximumDetailedTopologyVertices &&
                sourceTriangleCount <=
                    MaximumDetailedTopologyTriangles;

            if (detailedTopology)
            {
                VertexUnionFind union =
                    new VertexUnionFind(
                        vertices.Length);

                WeldCoincidentVertices(
                    vertices,
                    union,
                    accumulator.WeldTolerance);

                UnionTriangleTopology(
                    data,
                    vertices,
                    union);

                Dictionary<int, RawRegionAccumulator> regions =
                    new Dictionary<int, RawRegionAccumulator>();

                AccumulateTriangles(
                    data,
                    vertices,
                    union,
                    instanceKey,
                    regions,
                    accumulator);

                foreach (
                    KeyValuePair<int, RawRegionAccumulator> pair
                    in regions)
                {
                    RawRegionRecord region =
                        pair.Value.Build(
                            instanceKey,
                            accumulator);

                    if (region != null)
                        accumulator.AddRawRegion(region);
                }
            }
            else
            {
                accumulator.MarkRawRegionDetailTruncated();

                RawRegionAccumulator coarseRegion =
                    new RawRegionAccumulator();

                AccumulateTrianglesCoarse(
                    data,
                    vertices,
                    coarseRegion,
                    accumulator);

                RawRegionRecord region =
                    coarseRegion.Build(
                        instanceKey,
                        accumulator);

                if (region != null)
                    accumulator.AddRawRegion(region);
            }
        }

        private static void WeldCoincidentVertices(
            IReadOnlyList<Vector3> vertices,
            VertexUnionFind union,
            float tolerance)
        {
            if (vertices.Count <= 1)
                return;

            float safeTolerance =
                Math.Max(
                    0.000001f,
                    tolerance);

            float inverse =
                1f /
                safeTolerance;

            Dictionary<QuantizedPosition, List<int>> representativesByCell =
                new Dictionary<QuantizedPosition, List<int>>(
                    vertices.Count);

            float toleranceSquared =
                safeTolerance *
                safeTolerance;

            for (int index = 0;
                 index < vertices.Count;
                 index++)
            {
                Vector3 point =
                    vertices[index];

                if (!IsFinite(point))
                    continue;

                QuantizedPosition key =
                    new QuantizedPosition(
                        point,
                        inverse);

                bool welded =
                    false;

                for (int offsetX = -1;
                     offsetX <= 1;
                     offsetX++)
                {
                    for (int offsetY = -1;
                         offsetY <= 1;
                         offsetY++)
                    {
                        for (int offsetZ = -1;
                             offsetZ <= 1;
                             offsetZ++)
                        {
                            QuantizedPosition neighborKey =
                                key.Offset(
                                    offsetX,
                                    offsetY,
                                    offsetZ);

                            if (!representativesByCell.TryGetValue(
                                    neighborKey,
                                    out List<int> representatives))
                            {
                                continue;
                            }

                            for (int representativeIndex = 0;
                                 representativeIndex < representatives.Count;
                                 representativeIndex++)
                            {
                                int representative =
                                    representatives[representativeIndex];

                                if ((point -
                                     vertices[representative]).sqrMagnitude >
                                    toleranceSquared)
                                {
                                    continue;
                                }

                                union.Union(
                                    index,
                                    representative);

                                welded = true;
                            }
                        }
                    }
                }

                if (!representativesByCell.TryGetValue(
                        key,
                        out List<int> ownCell))
                {
                    ownCell =
                        new List<int>(1);

                    representativesByCell.Add(
                        key,
                        ownCell);
                }

                // Keep every non-equivalent representative in the cell. A single
                // representative is insufficient near quantization boundaries
                // because two distinct vertices can legitimately share one cell.
                if (!welded ||
                    ownCell.Count == 0)
                {
                    ownCell.Add(index);
                }
            }
        }
        private static void UnionTriangleTopology(
            Mesh.MeshData data,
            IReadOnlyList<Vector3> vertices,
            VertexUnionFind union)
        {
            for (int subMeshIndex = 0;
                 subMeshIndex < data.subMeshCount;
                 subMeshIndex++)
            {
                SubMeshDescriptor descriptor =
                    data.GetSubMesh(subMeshIndex);

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

                for (int triangleIndex = 0;
                     triangleIndex < triangleCount;
                     triangleIndex++)
                {
                    int offset =
                        triangleIndex * 3;

                    int a =
                        indices[offset];
                    int b =
                        indices[offset + 1];
                    int c =
                        indices[offset + 2];

                    if (!ValidVertexIndex(a, vertices.Count) ||
                        !ValidVertexIndex(b, vertices.Count) ||
                        !ValidVertexIndex(c, vertices.Count))
                    {
                        continue;
                    }

                    union.Union(a, b);
                    union.Union(b, c);
                }
            }
        }

        private static void AccumulateTriangles(
            Mesh.MeshData data,
            IReadOnlyList<Vector3> vertices,
            VertexUnionFind union,
            string instanceKey,
            IDictionary<int, RawRegionAccumulator> regions,
            TableSurfaceAccumulator accumulator)
        {
            for (int subMeshIndex = 0;
                 subMeshIndex < data.subMeshCount;
                 subMeshIndex++)
            {
                SubMeshDescriptor descriptor =
                    data.GetSubMesh(subMeshIndex);

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

                for (int triangleIndex = 0;
                     triangleIndex < triangleCount;
                     triangleIndex++)
                {
                    int offset =
                        triangleIndex * 3;

                    int indexA =
                        indices[offset];
                    int indexB =
                        indices[offset + 1];
                    int indexC =
                        indices[offset + 2];

                    if (!ValidVertexIndex(indexA, vertices.Count) ||
                        !ValidVertexIndex(indexB, vertices.Count) ||
                        !ValidVertexIndex(indexC, vertices.Count))
                    {
                        continue;
                    }

                    Vector3 a =
                        vertices[indexA];
                    Vector3 b =
                        vertices[indexB];
                    Vector3 c =
                        vertices[indexC];

                    if (!TryBuildTriangleDescriptor(
                            a,
                            b,
                            c,
                            out Vector3 centroid,
                            out Vector3 normalAbs,
                            out float area))
                    {
                        continue;
                    }

                    int root =
                        union.Find(indexA);

                    if (!regions.TryGetValue(
                            root,
                            out RawRegionAccumulator region))
                    {
                        region =
                            new RawRegionAccumulator();

                        regions.Add(
                            root,
                            region);
                    }

                    region.AddTriangle(
                        a,
                        b,
                        c,
                        centroid,
                        normalAbs,
                        area);

                    accumulator.AddTriangle(
                        a,
                        b,
                        c,
                        centroid,
                        normalAbs,
                        area);
                }
            }
        }

        private static long CountTriangleTopology(
            Mesh.MeshData data)
        {
            long total =
                0L;

            for (int subMeshIndex = 0;
                 subMeshIndex < data.subMeshCount;
                 subMeshIndex++)
            {
                SubMeshDescriptor descriptor =
                    data.GetSubMesh(subMeshIndex);

                if (descriptor.topology !=
                        MeshTopology.Triangles ||
                    descriptor.indexCount < 3)
                {
                    continue;
                }

                total +=
                    Math.Max(
                        0L,
                        (long)descriptor.indexCount /
                        3L);
            }

            return total;
        }

        private static void AccumulateTrianglesCoarse(
            Mesh.MeshData data,
            IReadOnlyList<Vector3> vertices,
            RawRegionAccumulator region,
            TableSurfaceAccumulator accumulator)
        {
            for (int subMeshIndex = 0;
                 subMeshIndex < data.subMeshCount;
                 subMeshIndex++)
            {
                SubMeshDescriptor descriptor =
                    data.GetSubMesh(subMeshIndex);

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

                for (int triangleIndex = 0;
                     triangleIndex < triangleCount;
                     triangleIndex++)
                {
                    int offset =
                        triangleIndex *
                        3;

                    int indexA =
                        indices[offset];

                    int indexB =
                        indices[offset + 1];

                    int indexC =
                        indices[offset + 2];

                    if (!ValidVertexIndex(indexA, vertices.Count) ||
                        !ValidVertexIndex(indexB, vertices.Count) ||
                        !ValidVertexIndex(indexC, vertices.Count))
                    {
                        continue;
                    }

                    Vector3 a =
                        vertices[indexA];

                    Vector3 b =
                        vertices[indexB];

                    Vector3 c =
                        vertices[indexC];

                    if (!TryBuildTriangleDescriptor(
                            a,
                            b,
                            c,
                            out Vector3 centroid,
                            out Vector3 normalAbs,
                            out float area))
                    {
                        continue;
                    }

                    region.AddTriangle(
                        a,
                        b,
                        c,
                        centroid,
                        normalAbs,
                        area);

                    accumulator.AddTriangle(
                        a,
                        b,
                        c,
                        centroid,
                        normalAbs,
                        area);
                }
            }
        }

        private static bool TryBuildTriangleDescriptor(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            out Vector3 centroid,
            out Vector3 normalAbs,
            out float area)
        {
            centroid =
                Vector3.zero;

            normalAbs =
                Vector3.zero;

            area =
                0f;

            if (!IsFinite(a) ||
                !IsFinite(b) ||
                !IsFinite(c))
            {
                return false;
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
                return false;
            }

            area =
                doubleArea *
                0.5f;

            normalAbs =
                new Vector3(
                    Math.Abs(cross.x),
                    Math.Abs(cross.y),
                    Math.Abs(cross.z)) /
                doubleArea;

            centroid =
                (a + b + c) /
                3f;

            return true;
        }

        private static SavicSemanticPartRecord BuildTabletopPart(
            TableSurfaceAccumulator accumulator,
            SavicModelAnalysisRecord model)
        {
            ZoneAccumulator zone =
                accumulator.UpperZone;

            if (!zone.HasGeometry)
                return null;

            GeometryDescriptor descriptor =
                zone.BuildDescriptor(
                    model,
                    accumulator.TotalArea);

            float footprintCoverage =
                Mathf.Sqrt(
                    Mathf.Clamp01(
                        descriptor.NormalizedSize.x) *
                    Mathf.Clamp01(
                        descriptor.NormalizedSize.z));

            float highPosition =
                Mathf.Clamp01(
                    (descriptor.NormalizedCenter.y -
                     0.55f) /
                    0.35f);

            float thinness =
                1f -
                Mathf.Clamp01(
                    descriptor.NormalizedSize.y /
                    0.55f);

            float horizontalEvidence =
                Mathf.Clamp01(
                    descriptor.MeanAbsoluteNormal.y);

            float areaStrength =
                Mathf.Clamp01(
                    descriptor.AreaFraction /
                    0.35f);

            float confidence =
                Mathf.Clamp01(
                    0.28f * footprintCoverage +
                    0.20f * highPosition +
                    0.18f * thinness +
                    0.22f * horizontalEvidence +
                    0.12f * areaStrength);

            string[] membership =
                accumulator.GetRegionMembership(
                    true);

            return BuildPart(
                "table.top",
                "Tabletop",
                confidence,
                true,
                false,
                membership,
                zone,
                descriptor,
                "Upper structural zone: footprint " +
                Format01(footprintCoverage) +
                ", height " +
                Format01(highPosition) +
                ", thinness " +
                Format01(thinness) +
                ", horizontal-normal evidence " +
                Format01(horizontalEvidence) +
                ".");
        }

        private static SavicSemanticPartRecord BuildSupportPart(
            TableSurfaceAccumulator accumulator,
            SavicModelAnalysisRecord model,
            SavicSupportPatternRecord pattern)
        {
            ZoneAccumulator zone =
                accumulator.LowerZone;

            if (!zone.HasGeometry)
                return null;

            GeometryDescriptor descriptor =
                zone.BuildDescriptor(
                    model,
                    accumulator.TotalArea);

            float heightCoverage =
                Mathf.Clamp01(
                    descriptor.NormalizedSize.y /
                    0.70f);

            float bottom01 =
                descriptor.NormalizedCenter.y -
                descriptor.NormalizedSize.y *
                0.5f;

            float groundEvidence =
                1f -
                Mathf.Clamp01(
                    bottom01 /
                    0.08f);

            float lowerPosition =
                1f -
                Mathf.Clamp01(
                    descriptor.NormalizedCenter.y /
                    0.62f);

            float verticalEvidence =
                1f -
                Mathf.Clamp01(
                    descriptor.MeanAbsoluteNormal.y);

            float areaStrength =
                Mathf.Clamp01(
                    descriptor.AreaFraction /
                    0.22f);

            float supportPatternEvidence =
                pattern != null &&
                pattern.analyzed
                    ? pattern.confidenceScore
                    : 0f;

            float confidence =
                Mathf.Clamp01(
                    0.20f * heightCoverage +
                    0.20f * groundEvidence +
                    0.15f * lowerPosition +
                    0.15f * verticalEvidence +
                    0.10f * areaStrength +
                    0.20f * supportPatternEvidence);

            string role =
                pattern != null &&
                string.Equals(
                    pattern.mode,
                    "MULTI_CONTACT",
                    StringComparison.Ordinal)
                    ? "LegSet"
                    : pattern != null &&
                      string.Equals(
                          pattern.mode,
                          "BROAD_BASE",
                          StringComparison.Ordinal)
                        ? "PedestalBase"
                        : "SupportStructure";

            string[] membership =
                accumulator.GetRegionMembership(
                    false);

            return BuildPart(
                "table.support",
                role,
                confidence,
                true,
                false,
                membership,
                zone,
                descriptor,
                "Lower structural zone: height " +
                Format01(heightCoverage) +
                ", ground contact " +
                Format01(groundEvidence) +
                ", vertical-normal evidence " +
                Format01(verticalEvidence) +
                ", support-pattern " +
                (pattern?.mode ?? "UNASSESSED") +
                " " +
                Format01(supportPatternEvidence) +
                ".");
        }

        private static SavicSemanticPartRecord BuildPart(
            string partId,
            string role,
            float confidence,
            bool syntheticZone,
            bool movableCandidate,
            IReadOnlyList<string> membership,
            ZoneAccumulator zone,
            GeometryDescriptor descriptor,
            string evidence)
        {
            return new SavicSemanticPartRecord
            {
                partId =
                    partId,
                role =
                    role,
                confidence =
                    ConfidenceBand(confidence),
                confidenceScore =
                    confidence,
                syntheticZone =
                    syntheticZone,
                movableCandidate =
                    movableCandidate,
                sourceRegionCount =
                    membership.Count,
                sourceRegionKeys =
                    string.Join(
                        ";",
                        membership),
                triangleCount =
                    zone.TriangleCount,
                areaFraction =
                    descriptor.AreaFraction,
                centerX =
                    descriptor.Center.x,
                centerY =
                    descriptor.Center.y,
                centerZ =
                    descriptor.Center.z,
                sizeX =
                    descriptor.Size.x,
                sizeY =
                    descriptor.Size.y,
                sizeZ =
                    descriptor.Size.z,
                normalizedCenterX =
                    descriptor.NormalizedCenter.x,
                normalizedCenterY =
                    descriptor.NormalizedCenter.y,
                normalizedCenterZ =
                    descriptor.NormalizedCenter.z,
                normalizedSizeX =
                    descriptor.NormalizedSize.x,
                normalizedSizeY =
                    descriptor.NormalizedSize.y,
                normalizedSizeZ =
                    descriptor.NormalizedSize.z,
                meanAbsoluteNormalX =
                    descriptor.MeanAbsoluteNormal.x,
                meanAbsoluteNormalY =
                    descriptor.MeanAbsoluteNormal.y,
                meanAbsoluteNormalZ =
                    descriptor.MeanAbsoluteNormal.z,
                evidence =
                    evidence
            };
        }
        private static SavicSupportPatternRecord AnalyzeSupportPattern(
            TableSurfaceAccumulator accumulator,
            SavicModelAnalysisRecord model)
        {
            SavicSupportPatternRecord result =
                new SavicSupportPatternRecord
                {
                    analyzed = true
                };

            IReadOnlyList<Vector2> points =
                accumulator.GroundContacts;

            if (points.Count == 0)
            {
                result.mode = "NO_CONTACT";
                result.confidence = "REVIEW";
                result.evidence =
                    "No contact vertices were found near the model support plane.";
                return result;
            }

            float width =
                Math.Max(
                    model.widthMeters,
                    0.0001f);

            float depth =
                Math.Max(
                    model.depthMeters,
                    0.0001f);

            float diagonal =
                Mathf.Sqrt(
                    width * width +
                    depth * depth);

            // Sparse low-poly feet may expose only their corner vertices.
            // Blend the Assets4All-style diagonal radius with a footprint-relative
            // floor so one physical foot remains one contact zone without merging
            // normally separated legs.
            float radius =
                Mathf.Clamp(
                    Math.Max(
                        diagonal * 0.035f,
                        Math.Min(width, depth) *
                        0.11f),
                    0.008f,
                    0.12f);

            List<List<Vector2>> clusters =
                ClusterGroundContacts(
                    points,
                    radius);

            FilterNegligibleClusters(
                clusters);

            result.zoneCount =
                clusters.Count;

            float minX =
                float.PositiveInfinity;
            float maxX =
                float.NegativeInfinity;
            float minZ =
                float.PositiveInfinity;
            float maxZ =
                float.NegativeInfinity;

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                Vector2 point =
                    points[index];

                minX =
                    Math.Min(minX, point.x);
                maxX =
                    Math.Max(maxX, point.x);
                minZ =
                    Math.Min(minZ, point.y);
                maxZ =
                    Math.Max(maxZ, point.y);
            }

            float spanX =
                Math.Max(
                    0f,
                    maxX - minX);

            float spanZ =
                Math.Max(
                    0f,
                    maxZ - minZ);

            result.supportSpanXRatio =
                Mathf.Clamp01(
                    spanX /
                    width);

            result.supportSpanZRatio =
                Mathf.Clamp01(
                    spanZ /
                    depth);

            result.broadBaseAreaRatio =
                Mathf.Clamp01(
                    (spanX * spanZ) /
                    Math.Max(
                        width * depth,
                        0.000001f));

            List<Vector2> centroids =
                new List<Vector2>(
                    clusters.Count);

            List<SavicSupportZoneRecord> supportZones =
                new List<SavicSupportZoneRecord>(
                    clusters.Count);

            float modelMinX =
                model.boundsCenterX -
                width * 0.5f;

            float modelMinZ =
                model.boundsCenterZ -
                depth * 0.5f;

            float maxClusterAreaRatio =
                0f;

            for (int index = 0;
                 index < clusters.Count;
                 index++)
            {
                List<Vector2> cluster =
                    clusters[index];

                Vector2 center =
                    Vector2.zero;

                float clusterMinX =
                    float.PositiveInfinity;
                float clusterMaxX =
                    float.NegativeInfinity;
                float clusterMinZ =
                    float.PositiveInfinity;
                float clusterMaxZ =
                    float.NegativeInfinity;

                for (int pointIndex = 0;
                     pointIndex < cluster.Count;
                     pointIndex++)
                {
                    Vector2 point =
                        cluster[pointIndex];

                    center += point;

                    clusterMinX =
                        Math.Min(clusterMinX, point.x);
                    clusterMaxX =
                        Math.Max(clusterMaxX, point.x);
                    clusterMinZ =
                        Math.Min(clusterMinZ, point.y);
                    clusterMaxZ =
                        Math.Max(clusterMaxZ, point.y);
                }

                center /=
                    Math.Max(
                        1,
                        cluster.Count);

                centroids.Add(center);

                float clusterWidth =
                    Math.Max(
                        0f,
                        clusterMaxX - clusterMinX);

                float clusterDepth =
                    Math.Max(
                        0f,
                        clusterMaxZ - clusterMinZ);

                float clusterAreaRatio =
                    (clusterWidth /
                     width) *
                    (clusterDepth /
                     depth);

                maxClusterAreaRatio =
                    Math.Max(
                        maxClusterAreaRatio,
                        clusterAreaRatio);

                float normalizedCenterX =
                    Mathf.Clamp01(
                        (center.x - modelMinX) /
                        width);

                float normalizedCenterZ =
                    Mathf.Clamp01(
                        (center.y - modelMinZ) /
                        depth);

                float normalizedSizeX =
                    Mathf.Clamp01(
                        clusterWidth /
                        width);

                float normalizedSizeZ =
                    Mathf.Clamp01(
                        clusterDepth /
                        depth);

                float zoneConfidence =
                    Mathf.Clamp01(
                        0.55f +
                        Math.Min(
                            0.35f,
                            cluster.Count * 0.045f) +
                        Math.Min(
                            0.10f,
                            (normalizedSizeX +
                             normalizedSizeZ) *
                            0.25f));

                supportZones.Add(
                    new SavicSupportZoneRecord
                    {
                        pointCount =
                            cluster.Count,
                        normalizedCenterX =
                            normalizedCenterX,
                        normalizedCenterZ =
                            normalizedCenterZ,
                        normalizedSizeX =
                            normalizedSizeX,
                        normalizedSizeZ =
                            normalizedSizeZ,
                        confidenceScore =
                            zoneConfidence,
                        evidence =
                            "Ground-contact cluster with " +
                            cluster.Count +
                            " unique point(s)."
                    });
            }

            supportZones.Sort(
                (left, right) =>
                {
                    int xOrder =
                        left.normalizedCenterX.CompareTo(
                            right.normalizedCenterX);

                    return xOrder != 0
                        ? xOrder
                        : left.normalizedCenterZ.CompareTo(
                            right.normalizedCenterZ);
                });

            result.zones.Clear();

            for (int zoneIndex = 0;
                 zoneIndex < supportZones.Count;
                 zoneIndex++)
            {
                SavicSupportZoneRecord zone =
                    supportZones[zoneIndex];

                zone.zoneId =
                    "support.zone." +
                    zoneIndex.ToString(
                        CultureInfo.InvariantCulture);

                result.zones.Add(zone);
            }

            List<Vector2> hull =
                BuildConvexHull(
                    centroids);

            float hullArea =
                PolygonArea(
                    hull);

            result.supportPolygonAreaRatio =
                Mathf.Clamp01(
                    hullArea /
                    Math.Max(
                        width * depth,
                        0.000001f));

            Vector2 modelCenter =
                new Vector2(
                    model.boundsCenterX,
                    model.boundsCenterZ);

            result.centerSupported =
                PointInPolygon(
                    modelCenter,
                    hull);

            bool multiContact =
                clusters.Count >= 3 &&
                result.supportSpanXRatio >= 0.34f &&
                result.supportSpanZRatio >= 0.34f &&
                result.supportPolygonAreaRatio >= 0.10f;

            bool broadBase =
                clusters.Count <= 2 &&
                maxClusterAreaRatio >= 0.07f &&
                result.supportSpanXRatio >= 0.25f &&
                result.supportSpanZRatio >= 0.25f;

            float zoneQuality =
                Mathf.Clamp01(
                    clusters.Count /
                    4f);

            float spanQuality =
                Mathf.Sqrt(
                    Math.Max(
                        0f,
                        result.supportSpanXRatio *
                        result.supportSpanZRatio));

            float polygonQuality =
                Mathf.Clamp01(
                    result.supportPolygonAreaRatio /
                    0.30f);

            float centerQuality =
                result.centerSupported
                    ? 1f
                    : 0.35f;

            if (multiContact)
            {
                result.mode =
                    "MULTI_CONTACT";

                result.confidenceScore =
                    Mathf.Clamp01(
                        0.28f * zoneQuality +
                        0.30f * spanQuality +
                        0.26f * polygonQuality +
                        0.16f * centerQuality);
            }
            else if (broadBase)
            {
                result.mode =
                    "BROAD_BASE";

                result.centerSupported =
                    minX <= modelCenter.x &&
                    modelCenter.x <= maxX &&
                    minZ <= modelCenter.y &&
                    modelCenter.y <= maxZ;

                result.confidenceScore =
                    Mathf.Clamp01(
                        0.50f *
                        Mathf.Clamp01(
                            maxClusterAreaRatio /
                            0.20f) +
                        0.30f * spanQuality +
                        0.20f *
                        (result.centerSupported
                            ? 1f
                            : 0.35f));
            }
            else
            {
                result.mode =
                    "WEAK_PATTERN";

                result.confidenceScore =
                    Mathf.Clamp01(
                        0.25f * zoneQuality +
                        0.35f * spanQuality +
                        0.25f * polygonQuality +
                        0.15f * centerQuality);
            }

            result.confidence =
                ConfidenceBand(
                    result.confidenceScore);

            result.evidence =
                result.mode +
                ": " +
                result.zoneCount +
                " support zone(s), span X/Z " +
                Format01(result.supportSpanXRatio) +
                "/" +
                Format01(result.supportSpanZRatio) +
                ", support polygon " +
                Format01(result.supportPolygonAreaRatio) +
                ", confidence " +
                Format01(result.confidenceScore) +
                ".";

            return result;
        }

        private static List<List<Vector2>> ClusterGroundContacts(
            IReadOnlyList<Vector2> points,
            float radius)
        {
            if (points.Count == 0)
                return new List<List<Vector2>>();

            float safeRadius =
                Math.Max(
                    radius,
                    0.000001f);

            float cellSize =
                safeRadius * 0.60f;

            float inverseCell =
                1f /
                cellSize;

            const int NeighborSpan = 2;
            const int MaximumRepresentatives = 4;

            VertexUnionFind union =
                new VertexUnionFind(
                    points.Count);

            Dictionary<GridCell2D, List<int>> buckets =
                new Dictionary<GridCell2D, List<int>>();

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                Vector2 point =
                    points[index];

                GridCell2D cell =
                    new GridCell2D(
                        point,
                        inverseCell);

                for (int dx = -NeighborSpan;
                     dx <= NeighborSpan;
                     dx++)
                {
                    for (int dz = -NeighborSpan;
                         dz <= NeighborSpan;
                         dz++)
                    {
                        GridCell2D neighbor =
                            new GridCell2D(
                                cell.X + dx,
                                cell.Z + dz);

                        if (!buckets.TryGetValue(
                                neighbor,
                                out List<int> representatives))
                        {
                            continue;
                        }

                        for (int repIndex = 0;
                             repIndex < representatives.Count;
                             repIndex++)
                        {
                            int other =
                                representatives[repIndex];

                            if (Vector2.Distance(
                                    point,
                                    points[other]) <=
                                safeRadius)
                            {
                                union.Union(
                                    index,
                                    other);
                                break;
                            }
                        }
                    }
                }

                if (!buckets.TryGetValue(
                        cell,
                        out List<int> bucket))
                {
                    bucket =
                        new List<int>(
                            MaximumRepresentatives);

                    buckets.Add(
                        cell,
                        bucket);
                }

                if (bucket.Count <
                    MaximumRepresentatives)
                {
                    bucket.Add(index);
                }
                else
                {
                    bucket[
                        index %
                        MaximumRepresentatives] =
                            index;
                }
            }

            Dictionary<int, List<Vector2>> grouped =
                new Dictionary<int, List<Vector2>>();

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                int root =
                    union.Find(index);

                if (!grouped.TryGetValue(
                        root,
                        out List<Vector2> group))
                {
                    group =
                        new List<Vector2>();

                    grouped.Add(
                        root,
                        group);
                }

                group.Add(
                    points[index]);
            }

            return new List<List<Vector2>>(
                grouped.Values);
        }

        private static void FilterNegligibleClusters(
            List<List<Vector2>> clusters)
        {
            if (clusters.Count <= 3)
                return;

            int maximumCount =
                0;

            for (int index = 0;
                 index < clusters.Count;
                 index++)
            {
                maximumCount =
                    Math.Max(
                        maximumCount,
                        clusters[index].Count);
            }

            int threshold =
                Math.Max(
                    1,
                    (int)(maximumCount * 0.08f));

            List<List<Vector2>> filtered =
                new List<List<Vector2>>();

            for (int index = 0;
                 index < clusters.Count;
                 index++)
            {
                if (clusters[index].Count >=
                    threshold)
                {
                    filtered.Add(
                        clusters[index]);
                }
            }

            if (filtered.Count >= 2)
            {
                clusters.Clear();
                clusters.AddRange(filtered);
            }
        }

        private static List<Vector2> BuildConvexHull(
            IReadOnlyList<Vector2> points)
        {
            List<Vector2> unique =
                new List<Vector2>();

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                bool exists = false;

                for (int other = 0;
                     other < unique.Count;
                     other++)
                {
                    if ((unique[other] -
                         points[index]).sqrMagnitude <=
                        0.0000000001f)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    unique.Add(points[index]);
            }

            unique.Sort(
                (left, right) =>
                {
                    int x =
                        left.x.CompareTo(
                            right.x);

                    return x != 0
                        ? x
                        : left.y.CompareTo(
                            right.y);
                });

            if (unique.Count <= 1)
                return unique;

            List<Vector2> lower =
                new List<Vector2>();

            for (int index = 0;
                 index < unique.Count;
                 index++)
            {
                Vector2 point =
                    unique[index];

                while (lower.Count >= 2 &&
                       Cross2D(
                           lower[lower.Count - 2],
                           lower[lower.Count - 1],
                           point) <= 0f)
                {
                    lower.RemoveAt(
                        lower.Count - 1);
                }

                lower.Add(point);
            }

            List<Vector2> upper =
                new List<Vector2>();

            for (int index = unique.Count - 1;
                 index >= 0;
                 index--)
            {
                Vector2 point =
                    unique[index];

                while (upper.Count >= 2 &&
                       Cross2D(
                           upper[upper.Count - 2],
                           upper[upper.Count - 1],
                           point) <= 0f)
                {
                    upper.RemoveAt(
                        upper.Count - 1);
                }

                upper.Add(point);
            }

            lower.RemoveAt(
                lower.Count - 1);

            upper.RemoveAt(
                upper.Count - 1);

            lower.AddRange(upper);
            return lower;
        }
        private static float PolygonArea(
            IReadOnlyList<Vector2> polygon)
        {
            if (polygon.Count < 3)
                return 0f;

            double area =
                0d;

            for (int index = 0;
                 index < polygon.Count;
                 index++)
            {
                Vector2 first =
                    polygon[index];

                Vector2 second =
                    polygon[
                        (index + 1) %
                        polygon.Count];

                area +=
                    (double)first.x *
                    second.y -
                    (double)second.x *
                    first.y;
            }

            return (float)(
                Math.Abs(area) *
                0.5d);
        }

        private static bool PointInPolygon(
            Vector2 point,
            IReadOnlyList<Vector2> polygon)
        {
            if (polygon.Count < 3)
                return false;

            bool inside =
                false;

            int previous =
                polygon.Count - 1;

            for (int current = 0;
                 current < polygon.Count;
                 current++)
            {
                Vector2 a =
                    polygon[current];

                Vector2 b =
                    polygon[previous];

                bool crosses =
                    (a.y > point.y) !=
                    (b.y > point.y);

                if (crosses)
                {
                    float denominator =
                        b.y - a.y;

                    if (Math.Abs(denominator) <
                        0.0000001f)
                    {
                        denominator =
                            denominator >= 0f
                                ? 0.0000001f
                                : -0.0000001f;
                    }

                    float intersectionX =
                        (b.x - a.x) *
                        (point.y - a.y) /
                        denominator +
                        a.x;

                    if (point.x <
                        intersectionX)
                    {
                        inside =
                            !inside;
                    }
                }

                previous =
                    current;
            }

            return inside;
        }

        private static float Cross2D(
            Vector2 origin,
            Vector2 a,
            Vector2 b)
        {
            return
                (a.x - origin.x) *
                (b.y - origin.y) -
                (a.y - origin.y) *
                (b.x - origin.x);
        }

        private static string BuildAnalysisEvidence(
            SavicSemanticPartAnalysisRecord analysis,
            SavicSemanticPartRecord tabletop,
            SavicSemanticPartRecord support,
            SavicSupportPatternRecord pattern)
        {
            return
                "raw regions " +
                analysis.rawRegionCount +
                " (" +
                analysis.regionDetailMode +
                "); semantic parts " +
                analysis.semanticPartCount +
                "; coverage " +
                Format01(
                    analysis.semanticCoverage) +
                "; tabletop " +
                (tabletop?.confidence ?? "MISSING") +
                " " +
                Format01(
                    tabletop?.confidenceScore ??
                    0f) +
                "; support " +
                (support?.confidence ?? "MISSING") +
                " " +
                Format01(
                    support?.confidenceScore ??
                    0f) +
                "; support pattern " +
                (pattern?.mode ?? "UNASSESSED") +
                " " +
                Format01(
                    pattern?.confidenceScore ??
                    0f) +
                ".";
        }

        private static string BuildHierarchyKey(
            Transform root,
            Transform target)
        {
            if (target == null)
                return "null";

            List<string> segments =
                new List<string>();

            Transform current =
                target;

            while (current != null &&
                   current != root)
            {
                segments.Add(
                    current.name +
                    "[" +
                    current.GetSiblingIndex() +
                    "]");

                current =
                    current.parent;
            }

            segments.Reverse();

            return segments.Count == 0
                ? "root"
                : string.Join(
                    "/",
                    segments);
        }

        private static bool ValidVertexIndex(
            int index,
            int count)
        {
            return
                index >= 0 &&
                index < count;
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

        private static string ConfidenceBand(
            float score)
        {
            if (score >= 0.80f)
                return "HIGH";

            if (score >= 0.62f)
                return "MEDIUM";

            if (score >= 0.45f)
                return "LOW";

            return "REVIEW";
        }

        private static string Format01(
            float value)
        {
            return
                Mathf.Clamp01(value)
                    .ToString(
                        "0.###",
                        CultureInfo.InvariantCulture);
        }

        private sealed class TableSurfaceAccumulator
        {
            private readonly SavicModelAnalysisRecord model;
            private readonly float minY;
            private readonly float height;
            private readonly float splitY;
            private readonly float groundY;
            private readonly Dictionary<GroundPointKey, Vector2>
                uniqueGroundContacts =
                    new Dictionary<GroundPointKey, Vector2>();

            internal TableSurfaceAccumulator(
                SavicModelAnalysisRecord model)
            {
                this.model =
                    model;

                minY =
                    model.boundsCenterY -
                    model.heightMeters *
                    0.5f;

                height =
                    Math.Max(
                        model.heightMeters,
                        0.0001f);

                splitY =
                    minY +
                    height *
                    TableVerticalSplit01;

                groundY =
                    minY +
                    height *
                    GroundBand01;

                float diagonal =
                    Mathf.Sqrt(
                        model.widthMeters *
                        model.widthMeters +
                        model.heightMeters *
                        model.heightMeters +
                        model.depthMeters *
                        model.depthMeters);

                WeldTolerance =
                    Mathf.Clamp(
                        diagonal *
                        0.00001f,
                        0.000001f,
                        0.00005f);

                GroundQuantization =
                    Math.Max(
                        0.0005f,
                        Math.Min(
                            Math.Max(
                                model.widthMeters,
                                model.depthMeters) *
                            0.0015f,
                            0.005f));
            }

            internal double TotalArea { get; private set; }
            internal long ValidTriangleCount { get; private set; }
            internal float WeldTolerance { get; }
            internal float GroundQuantization { get; }
            internal ZoneAccumulator UpperZone { get; } =
                new ZoneAccumulator();
            internal ZoneAccumulator LowerZone { get; } =
                new ZoneAccumulator();
            internal List<RawRegionRecord> RawRegions { get; } =
                new List<RawRegionRecord>();
            internal bool RawRegionDetailTruncated { get; private set; }
            internal IReadOnlyList<Vector2> GroundContacts =>
                new List<Vector2>(
                    uniqueGroundContacts.Values);

            internal void AddTriangle(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                Vector3 centroid,
                Vector3 normalAbs,
                float area)
            {
                TotalArea += area;
                ValidTriangleCount++;

                if (centroid.y >=
                    splitY)
                {
                    UpperZone.AddTriangle(
                        a,
                        b,
                        c,
                        centroid,
                        normalAbs,
                        area);
                }
                else
                {
                    LowerZone.AddTriangle(
                        a,
                        b,
                        c,
                        centroid,
                        normalAbs,
                        area);
                }

                AddGroundPoint(a);
                AddGroundPoint(b);
                AddGroundPoint(c);
            }

            internal void AddRawRegion(
                RawRegionRecord region)
            {
                if (region == null)
                    return;

                if (RawRegions.Count >=
                    MaximumStoredRawRegions)
                {
                    RawRegionDetailTruncated = true;
                    return;
                }

                RawRegions.Add(region);
            }

            internal void MarkRawRegionDetailTruncated()
            {
                RawRegionDetailTruncated = true;
            }

            internal void FinalizeRawRegions()
            {
                RawRegions.Sort(
                    (left, right) =>
                    {
                        int y =
                            left.Center.y.CompareTo(
                                right.Center.y);

                        if (y != 0)
                            return y;

                        int x =
                            left.Center.x.CompareTo(
                                right.Center.x);

                        if (x != 0)
                            return x;

                        int z =
                            left.Center.z.CompareTo(
                                right.Center.z);

                        if (z != 0)
                            return z;

                        return string.Compare(
                            left.SourceInstanceKey,
                            right.SourceInstanceKey,
                            StringComparison.Ordinal);
                    });

                for (int index = 0;
                     index < RawRegions.Count;
                     index++)
                {
                    RawRegionRecord region =
                        RawRegions[index];

                    region.Key =
                        BuildStableRegionKey(
                            region,
                            index,
                            model);
                }
            }

            internal string[] GetRegionMembership(
                bool upper)
            {
                List<string> keys =
                    new List<string>();

                float minimumArea =
                    (float)(
                        TotalArea *
                        MinimumRawRegionAreaFraction);

                for (int index = 0;
                     index < RawRegions.Count;
                     index++)
                {
                    RawRegionRecord region =
                        RawRegions[index];

                    if (region.Area <
                        minimumArea)
                    {
                        continue;
                    }

                    bool overlaps =
                        upper
                            ? region.Bounds.max.y >=
                              splitY
                            : region.Bounds.min.y <
                              splitY;

                    if (overlaps)
                        keys.Add(region.Key);
                }

                keys.Sort(
                    StringComparer.Ordinal);

                return keys.ToArray();
            }

            private void AddGroundPoint(
                Vector3 point)
            {
                if (point.y >
                    groundY)
                {
                    return;
                }

                GroundPointKey key =
                    new GroundPointKey(
                        point.x,
                        point.z,
                        GroundQuantization);

                if (!uniqueGroundContacts.ContainsKey(
                        key))
                {
                    uniqueGroundContacts.Add(
                        key,
                        new Vector2(
                            point.x,
                            point.z));
                }
            }

            private static string BuildStableRegionKey(
                RawRegionRecord region,
                int ordinal,
                SavicModelAnalysisRecord model)
            {
                Vector3 normalizedCenter =
                    NormalizeCenter(
                        region.Center,
                        model);

                Vector3 normalizedSize =
                    NormalizeSize(
                        region.Bounds.size,
                        model);

                string canonical =
                    string.Join(
                        "|",
                        new[]
                        {
                            "savic.raw-region.v1",
                            region.SourceInstanceKey ?? string.Empty,
                            normalizedCenter.x.ToString("0.000000", CultureInfo.InvariantCulture),
                            normalizedCenter.y.ToString("0.000000", CultureInfo.InvariantCulture),
                            normalizedCenter.z.ToString("0.000000", CultureInfo.InvariantCulture),
                            normalizedSize.x.ToString("0.000000", CultureInfo.InvariantCulture),
                            normalizedSize.y.ToString("0.000000", CultureInfo.InvariantCulture),
                            normalizedSize.z.ToString("0.000000", CultureInfo.InvariantCulture),
                            ordinal.ToString(CultureInfo.InvariantCulture)
                        });

                string digest =
                    SavicHashService.ComputeSha256Text(
                        canonical);

                return
                    "RG1-" +
                    digest.Substring(
                        0,
                        16);
            }
        }

        private sealed class ZoneAccumulator
        {
            private bool hasBounds;
            private Bounds bounds;
            private double area;
            private Vector3 weightedNormalAbs;
            private Vector3 weightedCentroid;

            internal bool HasGeometry =>
                TriangleCount > 0 &&
                area > 0d &&
                hasBounds;

            internal long TriangleCount { get; private set; }

            internal void AddTriangle(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                Vector3 centroid,
                Vector3 normalAbs,
                float triangleArea)
            {
                TriangleCount++;

                double weight =
                    triangleArea;

                area +=
                    weight;

                weightedNormalAbs +=
                    normalAbs *
                    triangleArea;

                weightedCentroid +=
                    centroid *
                    triangleArea;

                IncludePoint(a);
                IncludePoint(b);
                IncludePoint(c);
            }

            internal GeometryDescriptor BuildDescriptor(
                SavicModelAnalysisRecord model,
                double totalSurfaceArea)
            {
                Vector3 center =
                    area > 0d
                        ? weightedCentroid /
                          (float)area
                        : bounds.center;

                Vector3 normalAbs =
                    area > 0d
                        ? weightedNormalAbs /
                          (float)area
                        : Vector3.zero;

                float areaFraction =
                    totalSurfaceArea > 0d
                        ? Mathf.Clamp01(
                            (float)(
                                area /
                                totalSurfaceArea))
                        : 0f;

                return new GeometryDescriptor
                {
                    Center =
                        center,
                    Size =
                        bounds.size,
                    NormalizedCenter =
                        NormalizeCenter(
                            center,
                            model),
                    NormalizedSize =
                        NormalizeSize(
                            bounds.size,
                            model),
                    MeanAbsoluteNormal =
                        normalAbs,
                    AreaFraction =
                        areaFraction
                };
            }

            private void IncludePoint(
                Vector3 point)
            {
                if (!hasBounds)
                {
                    bounds =
                        new Bounds(
                            point,
                            Vector3.zero);

                    hasBounds =
                        true;

                    return;
                }

                bounds.Encapsulate(point);
            }
        }
        private sealed class RawRegionAccumulator
        {
            private bool hasBounds;
            private Bounds bounds;
            private double area;
            private Vector3 weightedCentroid;
            private Vector3 weightedNormalAbs;
            private long triangleCount;

            internal void AddTriangle(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                Vector3 centroid,
                Vector3 normalAbs,
                float triangleArea)
            {
                triangleCount++;
                area += triangleArea;

                weightedCentroid +=
                    centroid *
                    triangleArea;

                weightedNormalAbs +=
                    normalAbs *
                    triangleArea;

                Include(a);
                Include(b);
                Include(c);
            }

            internal RawRegionRecord Build(
                string sourceInstanceKey,
                TableSurfaceAccumulator accumulator)
            {
                if (!hasBounds ||
                    area <= 0d ||
                    triangleCount <= 0)
                {
                    return null;
                }

                return new RawRegionRecord
                {
                    SourceInstanceKey =
                        sourceInstanceKey,
                    Bounds =
                        bounds,
                    Center =
                        weightedCentroid /
                        (float)area,
                    MeanAbsoluteNormal =
                        weightedNormalAbs /
                        (float)area,
                    Area =
                        (float)area,
                    AreaFraction =
                        accumulator.TotalArea > 0d
                            ? (float)(
                                area /
                                accumulator.TotalArea)
                            : 0f,
                    TriangleCount =
                        triangleCount
                };
            }

            private void Include(
                Vector3 point)
            {
                if (!hasBounds)
                {
                    bounds =
                        new Bounds(
                            point,
                            Vector3.zero);

                    hasBounds =
                        true;

                    return;
                }

                bounds.Encapsulate(point);
            }
        }

        private sealed class RawRegionRecord
        {
            internal string Key = string.Empty;
            internal string SourceInstanceKey = string.Empty;
            internal Bounds Bounds;
            internal Vector3 Center;
            internal Vector3 MeanAbsoluteNormal;
            internal float Area;
            internal float AreaFraction;
            internal long TriangleCount;
        }

        private sealed class VertexUnionFind
        {
            private readonly int[] parent;
            private readonly byte[] rank;

            internal VertexUnionFind(
                int count)
            {
                parent =
                    new int[
                        Math.Max(
                            0,
                            count)];

                rank =
                    new byte[
                        parent.Length];

                for (int index = 0;
                     index < parent.Length;
                     index++)
                {
                    parent[index] =
                        index;
                }
            }

            internal int Find(
                int value)
            {
                int root =
                    value;

                while (parent[root] !=
                       root)
                {
                    root =
                        parent[root];
                }

                while (parent[value] !=
                       value)
                {
                    int next =
                        parent[value];

                    parent[value] =
                        root;

                    value =
                        next;
                }

                return root;
            }

            internal void Union(
                int left,
                int right)
            {
                int a =
                    Find(left);

                int b =
                    Find(right);

                if (a == b)
                    return;

                if (rank[a] <
                    rank[b])
                {
                    int temporary =
                        a;

                    a =
                        b;

                    b =
                        temporary;
                }

                parent[b] =
                    a;

                if (rank[a] ==
                    rank[b])
                {
                    rank[a]++;
                }
            }
        }

        private readonly struct QuantizedPosition :
            IEquatable<QuantizedPosition>
        {
            internal QuantizedPosition(
                Vector3 point,
                float inverseTolerance)
            {
                X =
                    Quantize(
                        point.x,
                        inverseTolerance);

                Y =
                    Quantize(
                        point.y,
                        inverseTolerance);

                Z =
                    Quantize(
                        point.z,
                        inverseTolerance);
            }

            private QuantizedPosition(
                long x,
                long y,
                long z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            private long X { get; }
            private long Y { get; }
            private long Z { get; }

            internal QuantizedPosition Offset(
                int x,
                int y,
                int z)
            {
                return new QuantizedPosition(
                    X + x,
                    Y + y,
                    Z + z);
            }

            public bool Equals(
                QuantizedPosition other)
            {
                return
                    X == other.X &&
                    Y == other.Y &&
                    Z == other.Z;
            }

            public override bool Equals(
                object obj)
            {
                return
                    obj is QuantizedPosition other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash =
                        X.GetHashCode();

                    hash =
                        hash * 397 ^
                        Y.GetHashCode();

                    hash =
                        hash * 397 ^
                        Z.GetHashCode();

                    return hash;
                }
            }

            private static long Quantize(
                float value,
                float inverseTolerance)
            {
                return (long)Math.Round(
                    value *
                    inverseTolerance,
                    MidpointRounding.AwayFromZero);
            }
        }

        private readonly struct GroundPointKey :
            IEquatable<GroundPointKey>
        {
            internal GroundPointKey(
                float x,
                float z,
                float quantization)
            {
                X =
                    (long)Math.Round(
                        x /
                        quantization,
                        MidpointRounding.AwayFromZero);

                Z =
                    (long)Math.Round(
                        z /
                        quantization,
                        MidpointRounding.AwayFromZero);
            }

            private long X { get; }
            private long Z { get; }

            public bool Equals(
                GroundPointKey other)
            {
                return
                    X == other.X &&
                    Z == other.Z;
            }

            public override bool Equals(
                object obj)
            {
                return
                    obj is GroundPointKey other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        X.GetHashCode() *
                        397 ^
                        Z.GetHashCode();
                }
            }
        }

        private readonly struct GridCell2D :
            IEquatable<GridCell2D>
        {
            internal GridCell2D(
                Vector2 point,
                float inverseCell)
            {
                X =
                    Mathf.FloorToInt(
                        point.x *
                        inverseCell);

                Z =
                    Mathf.FloorToInt(
                        point.y *
                        inverseCell);
            }

            internal GridCell2D(
                int x,
                int z)
            {
                X = x;
                Z = z;
            }

            internal int X { get; }
            internal int Z { get; }

            public bool Equals(
                GridCell2D other)
            {
                return
                    X == other.X &&
                    Z == other.Z;
            }

            public override bool Equals(
                object obj)
            {
                return
                    obj is GridCell2D other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        X * 397 ^
                        Z;
                }
            }
        }

        private sealed class GeometryDescriptor
        {
            internal Vector3 Center;
            internal Vector3 Size;
            internal Vector3 NormalizedCenter;
            internal Vector3 NormalizedSize;
            internal Vector3 MeanAbsoluteNormal;
            internal float AreaFraction;
        }

        private static Vector3 NormalizeCenter(
            Vector3 center,
            SavicModelAnalysisRecord model)
        {
            Vector3 min =
                new Vector3(
                    model.boundsCenterX -
                    model.widthMeters *
                    0.5f,
                    model.boundsCenterY -
                    model.heightMeters *
                    0.5f,
                    model.boundsCenterZ -
                    model.depthMeters *
                    0.5f);

            return new Vector3(
                SafeRatio(
                    center.x -
                    min.x,
                    model.widthMeters),
                SafeRatio(
                    center.y -
                    min.y,
                    model.heightMeters),
                SafeRatio(
                    center.z -
                    min.z,
                    model.depthMeters));
        }

        private static Vector3 NormalizeSize(
            Vector3 size,
            SavicModelAnalysisRecord model)
        {
            return new Vector3(
                SafeRatio(
                    size.x,
                    model.widthMeters),
                SafeRatio(
                    size.y,
                    model.heightMeters),
                SafeRatio(
                    size.z,
                    model.depthMeters));
        }

        private static float SafeRatio(
            float numerator,
            float denominator)
        {
            return
                Math.Abs(denominator) >
                0.000001f
                    ? numerator /
                      denominator
                    : 0f;
        }
    }
}
