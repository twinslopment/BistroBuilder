using System;
using System.Globalization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicBarStoolGeometryRecord
    {
        public bool usable;
        public string analyzerVersion = SavicBarStoolGeometryAnalyzer.Version;
        public float seatHeightMeters;
        public Vector3 seatCenterMetric;
        public Vector3 seatSizeMetric;
        public float seatProjectedCoverage;
        public float seatSurfaceFill;
        public float lowerSupportAreaRatio;
        public long triangleCount;
        public bool facingUsable;
        public bool hasBackrest;
        public Vector3 facingMetric;
        public string facingEvidence = string.Empty;
        public string evidence = string.Empty;
    }

    // Geometry evidence only. This does not grant classification, runtime seating or publication.
    internal static class SavicBarStoolGeometryAnalyzer
    {
        internal const string Version = "1.1.0";
        private const int BinCount = 64;
        private const long TriangleBudget = 2000000;

        internal static SavicBarStoolGeometryRecord Analyze(GameObject root, SavicModelAnalysisRecord model)
        {
            SavicBarStoolGeometryRecord result = new SavicBarStoolGeometryRecord();
            if (root == null || model == null || !model.analyzed || !model.hasUsableBounds ||
                model.hasSkinnedMeshes || model.hasNegativeScale || !Positive(model.heightMeters) ||
                !Positive(model.widthMeters) || !Positive(model.depthMeters) ||
                model.heightMeters < 0.45f || model.heightMeters > 2.5f ||
                model.heightMeters < Math.Max(model.widthMeters, model.depthMeters) * 0.8f ||
                !Finite(new Vector3(model.boundsCenterX, model.boundsCenterY, model.boundsCenterZ)) ||
                model.triangleCount <= 0 || model.triangleCount > TriangleBudget)
            { result.evidence = "Static, finite model bounds and a bounded triangle count are required."; return result; }

            Band[] bands = new Band[BinCount];
            for (int index = 0; index < BinCount; index++) bands[index] = new Band();
            float bottom = model.boundsCenterY - model.heightMeters * 0.5f;
            double totalArea = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                using Mesh.MeshDataArray meshes = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                Mesh.MeshData mesh = meshes[0];
                using NativeArray<Vector3> vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp);
                mesh.GetVertices(vertices);
                Matrix4x4 metric = SavicMetricSpace.LocalToMetric(root.transform, filter.transform);
                foreach (Vector3 vertex in vertices)
                    if (!Finite(metric.MultiplyPoint3x4(vertex)))
                    { result.evidence = "Mesh contains invalid vertices."; return result; }
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    SubMeshDescriptor descriptor = mesh.GetSubMesh(submesh);
                    if (descriptor.topology != MeshTopology.Triangles) continue;
                    using NativeArray<int> indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                    mesh.GetIndices(indices, submesh, true);
                    for (int index = 0; index + 2 < indices.Length; index += 3)
                    {
                        int ia = indices[index], ib = indices[index + 1], ic = indices[index + 2];
                        if ((uint)ia >= vertices.Length || (uint)ib >= vertices.Length || (uint)ic >= vertices.Length)
                        { result.evidence = "Mesh contains invalid triangle indices."; return result; }
                        if (++result.triangleCount > TriangleBudget)
                        { result.evidence = "Geometry exceeds the analysis budget."; return result; }
                        Vector3 a = metric.MultiplyPoint3x4(vertices[ia]);
                        Vector3 b = metric.MultiplyPoint3x4(vertices[ib]);
                        Vector3 c = metric.MultiplyPoint3x4(vertices[ic]);
                        Vector3 cross = Vector3.Cross(b - a, c - a);
                        float doubleArea = cross.magnitude;
                        if (!Positive(doubleArea)) continue;
                        Vector3 centroid = (a + b + c) / 3;
                        float height01 = (centroid.y - bottom) / model.heightMeters;
                        int bin = Mathf.Clamp(Mathf.FloorToInt(height01 * BinCount), 0, BinCount - 1);
                        double area = doubleArea * 0.5;
                        bands[bin].AllArea += area;
                        bands[bin].AllXWeighted += centroid.x * area;
                        bands[bin].AllZWeighted += centroid.z * area;
                        totalArea += area;
                        // Winding matters: a lower facing surface is not evidence of a seat top.
                        if (cross.y / doubleArea >= 0.65f)
                            bands[bin].AddUpward(a, b, c, centroid, cross.y * 0.5);
                    }
                }
            }
            double footprint = model.widthMeters * model.depthMeters;
            if (totalArea <= 0 || footprint <= 0)
            { result.evidence = "No measurable surfaces were found."; return result; }

            // Prefer a broad upper surface over a curved cushion's small apex.
            // Narrow foot rings fail projected coverage and surface fill.
            for (int bin = BinCount - 1; bin >= BinCount * 0.5f; bin--)
            {
                Band window = new Band();
                for (int neighbor = Math.Max(0, bin - 1); neighbor <= Math.Min(BinCount - 1, bin + 1); neighbor++)
                    window.Merge(bands[neighbor]);
                if (!window.HasBounds || window.Projected <= 0) continue;
                Bounds bounds = window.Bounds;
                double coverage = window.Projected / footprint;
                double rectangle = bounds.size.x * bounds.size.z;
                double fill = rectangle > 0 ? window.Projected / rectangle : 0;
                float height = (float)(window.HeightWeighted / window.Projected) - bottom;
                float seatHeight01 = height / model.heightMeters;
                if (coverage < 0.15 || fill < 0.30 ||
                    bounds.size.x < model.widthMeters * 0.40f || bounds.size.z < model.depthMeters * 0.40f ||
                    bounds.size.y > model.heightMeters * 0.12f || seatHeight01 < 0.5f) continue;

                double support = 0;
                int supportTop = Mathf.Clamp(Mathf.FloorToInt((seatHeight01 - 0.08f) * BinCount), 0, BinCount - 1);
                for (int index = 0; index <= supportTop; index++) support += bands[index].AllArea;
                if (support / totalArea < 0.10) continue;
                if (result.usable && coverage <= result.seatProjectedCoverage) continue;
                result.usable = true;
                result.seatHeightMeters = height;
                result.seatCenterMetric = new Vector3((float)(window.XWeighted / window.Projected),
                    bottom + height, (float)(window.ZWeighted / window.Projected));
                result.seatSizeMetric = bounds.size;
                result.seatProjectedCoverage = (float)coverage;
                result.seatSurfaceFill = (float)fill;
                result.lowerSupportAreaRatio = (float)(support / totalArea);
                result.evidence = string.Format(CultureInfo.InvariantCulture,
                    "Measured upper seat at {0:0.###} m ({1:0.###} of source height); projected coverage={2:0.###}, " +
                    "surface fill={3:0.###}, lower support area={4:0.###}. No physical scale, facing or runtime use inferred.",
                    height, seatHeight01, coverage, fill, result.lowerSupportAreaRatio);
            }
            if (result.usable)
            {
                if (model.heightMeters - result.seatHeightMeters <= model.heightMeters * 0.08f)
                {
                    result.facingUsable = true;
                    result.facingMetric = Vector3.forward;
                    result.facingEvidence = "No substantial geometry above the seat; +Z is an explicit authored convention, not an inferred original front.";
                }
                else
                {
                    double upperArea = 0, upperX = 0, upperZ = 0;
                    int firstUpper = Mathf.CeilToInt((result.seatHeightMeters / model.heightMeters + 0.08f) * BinCount);
                    for (int index = firstUpper; index < BinCount; index++)
                    { upperArea += bands[index].AllArea; upperX += bands[index].AllXWeighted; upperZ += bands[index].AllZWeighted; }
                    Vector3 offset = upperArea > 0 ? new Vector3((float)(upperX / upperArea) - result.seatCenterMetric.x, 0,
                        (float)(upperZ / upperArea) - result.seatCenterMetric.z) : Vector3.zero;
                    result.hasBackrest = true;
                    result.facingUsable = upperArea / totalArea >= 0.02 && offset.magnitude >=
                        Math.Min(result.seatSizeMetric.x, result.seatSizeMetric.z) * 0.2f;
                    if (result.facingUsable) result.facingMetric = -offset.normalized;
                    result.facingEvidence = result.facingUsable ? "Unilateral upper structure gives the back; front is opposite its area-weighted horizontal offset: " + offset
                        : "Upper structure is insufficient or symmetric around the seat; no reliable front inferred.";
                }
                return result;
            }
            result.evidence = "No broad, filled upper seating surface with lower support was found; narrow footrests are insufficient.";
            return result;
        }

        private static bool Positive(float value) => value > 0.000000001f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private sealed class Band
        {
            internal double AllArea, AllXWeighted, AllZWeighted, Projected, HeightWeighted, XWeighted, ZWeighted;
            internal bool HasBounds;
            internal Bounds Bounds;
            internal void AddUpward(Vector3 a, Vector3 b, Vector3 c, Vector3 centroid, double projected)
            {
                Projected += projected;
                HeightWeighted += centroid.y * projected;
                XWeighted += centroid.x * projected;
                ZWeighted += centroid.z * projected;
                Include(a); Include(b); Include(c);
            }
            private void Include(Vector3 point)
            {
                if (HasBounds) Bounds.Encapsulate(point);
                else { Bounds = new Bounds(point, Vector3.zero); HasBounds = true; }
            }
            internal void Merge(Band band)
            {
                AllArea += band.AllArea; Projected += band.Projected;
                HeightWeighted += band.HeightWeighted; XWeighted += band.XWeighted; ZWeighted += band.ZWeighted;
                if (band.HasBounds) { Include(band.Bounds.min); Include(band.Bounds.max); }
            }
        }
    }
}
