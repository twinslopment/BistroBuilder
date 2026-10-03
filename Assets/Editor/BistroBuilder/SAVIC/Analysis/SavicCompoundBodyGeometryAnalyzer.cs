using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicCompoundBodyBox
    {
        public Vector3 centerMetric;
        public Vector2 sizeMetric;
    }

    [Serializable]
    internal sealed class SavicUpperSurfaceBand
    {
        public float heightMeters;
        public float coverage;
    }

    [Serializable]
    internal sealed class SavicCompoundBodyGeometryRecord
    {
        public bool usable;
        public bool hasAccessibleInterior;
        public string analyzerVersion = SavicCompoundBodyGeometryAnalyzer.Version;
        public int columns, rows, occupiedCells;
        public float cellSizeMeters;
        public long triangleCount;
        public Vector3 interiorPointMetric;
        public Vector3 openingDirectionMetric;
        public float interiorClearanceMeters;
        public bool hasBroadUpperSurface;
        public float upperSurfaceHeightMeters;
        public float upperSurfaceCoverage;
        public float dominantUpperSurfaceHeightMeters;
        public float dominantUpperSurfaceCoverage;
        public List<SavicUpperSurfaceBand> upperSurfaceBands = new List<SavicUpperSurfaceBand>();
        public List<SavicCompoundBodyBox> boxes = new List<SavicCompoundBodyBox>();
        public string evidence = string.Empty;
        // Diagnostic grid, not a runtime authority or persisted content definition.
        [NonSerialized] public bool[] occupancy;
    }

    /// <summary>
    /// Conservative XZ projection of all static mesh triangles, without bounding-box filling.
    /// Produces root-metric boxes and measures accessible concavities; grants no publication.
    /// </summary>
    internal static class SavicCompoundBodyGeometryAnalyzer
    {
        internal const string Version = "1.2.0";
        private const int Resolution = 48;
        private const int HeightBands = 64;
        private const long TriangleBudget = 4000000;

        internal static SavicCompoundBodyGeometryRecord Analyze(GameObject root, SavicModelAnalysisRecord model)
        {
            SavicCompoundBodyGeometryRecord result = new SavicCompoundBodyGeometryRecord();
            if (root == null || model == null || !model.analyzed || !model.hasUsableBounds ||
                model.hasSkinnedMeshes || model.hasNegativeScale ||
                !Positive(model.widthMeters) || !Positive(model.depthMeters) || !Positive(model.heightMeters) ||
                model.triangleCount <= 0 || model.triangleCount > TriangleBudget ||
                !Finite(new Vector3(model.boundsCenterX, model.boundsCenterY, model.boundsCenterZ)))
            { result.evidence = "Finite, bounded static source geometry is required."; return result; }

            float minX = model.boundsCenterX - model.widthMeters * 0.5f;
            float minZ = model.boundsCenterZ - model.depthMeters * 0.5f;
            float bottom = model.boundsCenterY - model.heightMeters * 0.5f;
            result.cellSizeMeters = Mathf.Max(model.widthMeters, model.depthMeters) / Resolution;
            result.columns = Mathf.Clamp(Mathf.CeilToInt(model.widthMeters / result.cellSizeMeters), 1, Resolution);
            result.rows = Mathf.Clamp(Mathf.CeilToInt(model.depthMeters / result.cellSizeMeters), 1, Resolution);
            result.occupancy = new bool[result.columns * result.rows];
            double[] upperAreas = new double[HeightBands];
            double[] upperHeightSums = new double[HeightBands];
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                using Mesh.MeshDataArray meshes = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                Mesh.MeshData mesh = meshes[0];
                using NativeArray<Vector3> vertexStorage = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp);
                // Mutable view of the owned copy; disposing vertexStorage releases the allocation.
                NativeArray<Vector3> vertices = vertexStorage;
                mesh.GetVertices(vertices);
                Matrix4x4 metric = SavicMetricSpace.LocalToMetric(root.transform, filter.transform);
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 transformed = metric.MultiplyPoint3x4(vertices[i]);
                    if (!Finite(transformed)) { result.evidence = "Source has non-finite vertices."; return result; }
                    vertices[i] = transformed;
                }
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    SubMeshDescriptor descriptor = mesh.GetSubMesh(submesh);
                    if (descriptor.topology != MeshTopology.Triangles) continue;
                    using NativeArray<int> indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                    mesh.GetIndices(indices, submesh, true);
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                        if ((uint)ia >= vertices.Length || (uint)ib >= vertices.Length || (uint)ic >= vertices.Length ||
                            ++result.triangleCount > TriangleBudget)
                        { result.evidence = "Source indices or triangle budget are invalid."; return result; }
                        Vector2 a = XZ(vertices[ia]), b = XZ(vertices[ib]), c = XZ(vertices[ic]);
                        Rasterize(a, b, c, minX, minZ, result);
                        MeasureUpperTriangle(vertices[ia], vertices[ib], vertices[ic], bottom, model.heightMeters,
                            upperAreas, upperHeightSums);
                    }
                }
            }
            foreach (bool occupied in result.occupancy) if (occupied) result.occupiedCells++;
            // Model analysis counts unique mesh resources; this projection counts every instance.
            if (result.occupiedCells == 0 || result.triangleCount < model.triangleCount)
            { result.evidence = "Measured geometry is empty or omits source mesh resources."; return result; }
            Decompose(minX, minZ, bottom, model.widthMeters, model.depthMeters, result);
            if (result.boxes.Count > BistroBuilderSpatialPhysicalFootprintAdapter.MaximumStaticParts)
            { result.evidence = "Conservative body exceeds the runtime static-part budget."; return result; }
            MeasureInterior(minX, minZ, bottom, result);
            ResolveUpperSurface(model, upperAreas, upperHeightSums, result);
            result.usable = true;
            result.evidence = "Conservative projection of " + result.triangleCount + " triangles into " +
                result.boxes.Count + " static boxes; occupied cells=" + result.occupiedCells +
                ", accessible concavity=" + result.hasAccessibleInterior +
                ", broad upper surface=" + result.hasBroadUpperSurface +
                ". Source-unit geometry only; no physical scale, service points or publication inferred.";
            return result;
        }

        private static void MeasureUpperTriangle(Vector3 a, Vector3 b, Vector3 c, float bottom, float height,
            double[] areas, double[] heightSums)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            float length = cross.magnitude;
            if (!Finite(cross) || !Positive(length) || cross.y / length < 0.75f) return;
            float surfaceHeight = ((a.y - bottom) + (b.y - bottom) + (c.y - bottom)) / 3f;
            if (surfaceHeight < height * 0.5f || surfaceHeight > height + 0.00001f) return;
            int band = Mathf.Clamp(Mathf.FloorToInt(surfaceHeight / height * HeightBands), 0, HeightBands - 1);
            double area = cross.y * 0.5;
            areas[band] += area;
            heightSums[band] += area * surfaceHeight;
        }

        private static void ResolveUpperSurface(SavicModelAnalysisRecord model, double[] areas, double[] heights,
            SavicCompoundBodyGeometryRecord result)
        {
            for (int band = 0; band < HeightBands; band++)
                if (areas[band] > 0)
                    result.upperSurfaceBands.Add(new SavicUpperSurfaceBand {
                        heightMeters = (float)(heights[band] / areas[band]),
                        coverage = (float)(areas[band] / ((double)model.widthMeters * model.depthMeters)) });
            // Dominant broad upward surface, rather than the tallest small fixture/apex.
            double strongest = 0, weightedHeight = 0;
            for (int center = HeightBands / 2; center < HeightBands; center++)
            {
                double area = 0, sum = 0;
                for (int band = Math.Max(0, center - 1); band <= Math.Min(HeightBands - 1, center + 1); band++)
                { area += areas[band]; sum += heights[band]; }
                if (area <= strongest) continue;
                strongest = area; weightedHeight = sum;
            }
            double coverage = strongest / ((double)model.widthMeters * model.depthMeters);
            if (strongest <= 0 || double.IsNaN(coverage) || double.IsInfinity(coverage)) return;
            result.dominantUpperSurfaceHeightMeters = (float)(weightedHeight / strongest);
            result.dominantUpperSurfaceCoverage = (float)coverage;
            // Tiered bars can have a wider lower worktop and a narrower upper customer ledge.
            // Use the highest genuinely broad level; tiny taps/apices cannot qualify.
            for (int center = HeightBands / 2; center < HeightBands; center++)
            {
                double area = 0, sum = 0;
                for (int band = Math.Max(0, center - 1); band <= Math.Min(HeightBands - 1, center + 1); band++)
                { area += areas[band]; sum += heights[band]; }
                double levelCoverage = area / ((double)model.widthMeters * model.depthMeters);
                if (levelCoverage < 0.08 || area < strongest * 0.2) continue;
                float levelHeight = (float)(sum / area);
                if (levelHeight <= result.upperSurfaceHeightMeters) continue;
                result.hasBroadUpperSurface = true;
                result.upperSurfaceHeightMeters = levelHeight;
                result.upperSurfaceCoverage = (float)levelCoverage;
            }
        }

        private static void Rasterize(Vector2 a, Vector2 b, Vector2 c, float minX, float minZ,
            SavicCompoundBodyGeometryRecord result)
        {
            float cell = result.cellSizeMeters;
            int left = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - minX) / cell), 0, result.columns - 1);
            int right = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - minX) / cell), 0, result.columns - 1);
            int lower = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - minZ) / cell), 0, result.rows - 1);
            int upper = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - minZ) / cell), 0, result.rows - 1);
            for (int z = lower; z <= upper; z++)
            for (int x = left; x <= right; x++)
            {
                int index = z * result.columns + x;
                if (result.occupancy[index]) continue;
                Vector2 center = new Vector2(minX + (x + 0.5f) * cell, minZ + (z + 0.5f) * cell);
                if (!Separated(a, b, c, center, cell * 0.5f, Normal(b - a)) &&
                    !Separated(a, b, c, center, cell * 0.5f, Normal(c - b)) &&
                    !Separated(a, b, c, center, cell * 0.5f, Normal(a - c)))
                    result.occupancy[index] = true;
            }
        }

        private static bool Separated(Vector2 a, Vector2 b, Vector2 c, Vector2 center, float half, Vector2 axis)
        {
            if (axis.sqrMagnitude < 0.000000000001f) return false;
            float pa = Vector2.Dot(a - center, axis), pb = Vector2.Dot(b - center, axis), pc = Vector2.Dot(c - center, axis);
            float radius = (Mathf.Abs(axis.x) + Mathf.Abs(axis.y)) * half;
            return Mathf.Min(pa, Mathf.Min(pb, pc)) > radius || Mathf.Max(pa, Mathf.Max(pb, pc)) < -radius;
        }

        private static void Decompose(float minX, float minZ, float bottom, float width, float depth,
            SavicCompoundBodyGeometryRecord result)
        {
            List<Run> active = new List<Run>();
            for (int z = 0; z < result.rows; z++)
            {
                List<Run> next = new List<Run>();
                for (int x = 0; x < result.columns; x++)
                {
                    if (!result.occupancy[z * result.columns + x]) continue;
                    int start = x;
                    while (x + 1 < result.columns && result.occupancy[z * result.columns + x + 1]) x++;
                    Run run = active.Find(value => value.left == start && value.right == x);
                    if (run == null) run = new Run { left = start, right = x, bottom = z, top = z };
                    else { active.Remove(run); run.top = z; }
                    next.Add(run);
                }
                foreach (Run run in active) AddBox(run, minX, minZ, bottom, width, depth, result);
                active = next;
            }
            foreach (Run run in active) AddBox(run, minX, minZ, bottom, width, depth, result);
        }
        private static void AddBox(Run run, float minX, float minZ, float bottom, float width, float depth,
            SavicCompoundBodyGeometryRecord result)
        {
            float x0 = minX + run.left * result.cellSizeMeters;
            float x1 = minX + Mathf.Min(width, (run.right + 1) * result.cellSizeMeters);
            float z0 = minZ + run.bottom * result.cellSizeMeters;
            float z1 = minZ + Mathf.Min(depth, (run.top + 1) * result.cellSizeMeters);
            result.boxes.Add(new SavicCompoundBodyBox {
                centerMetric = new Vector3((x0 + x1) * 0.5f, bottom, (z0 + z1) * 0.5f),
                sizeMetric = new Vector2(x1 - x0, z1 - z0) });
        }

        private static void MeasureInterior(float minX, float minZ, float bottom, SavicCompoundBodyGeometryRecord result)
        {
            // Flood from open boundary cells. An enclosed hole is not evidence of waiter access.
            bool[] accessible = new bool[result.occupancy.Length];
            Queue<int> queue = new Queue<int>();
            for (int z = 0; z < result.rows; z++)
            for (int x = 0; x < result.columns; x++)
                if ((x == 0 || z == 0 || x == result.columns - 1 || z == result.rows - 1) &&
                    !result.occupancy[z * result.columns + x])
                { int key = z * result.columns + x; accessible[key] = true; queue.Enqueue(key); }
            int[] dx = { -1, 1, 0, 0 }, dz = { 0, 0, -1, 1 };
            while (queue.Count > 0)
            {
                int key = queue.Dequeue(), x = key % result.columns, z = key / result.columns;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + dx[direction], nz = z + dz[direction];
                    if (nx < 0 || nz < 0 || nx >= result.columns || nz >= result.rows) continue;
                    int next = nz * result.columns + nx;
                    if (!accessible[next] && !result.occupancy[next]) { accessible[next] = true; queue.Enqueue(next); }
                }
            }
            for (int z = 1; z < result.rows - 1; z++)
            for (int x = 1; x < result.columns - 1; x++)
            {
                if (!accessible[z * result.columns + x]) continue;
                int blockedDirections = 0, opening = -1;
                for (int direction = 0; direction < 4; direction++)
                {
                    bool blocked = false;
                    for (int nx = x + dx[direction], nz = z + dz[direction];
                         nx >= 0 && nz >= 0 && nx < result.columns && nz < result.rows;
                         nx += dx[direction], nz += dz[direction])
                        if (result.occupancy[nz * result.columns + nx]) { blocked = true; break; }
                    if (blocked) blockedDirections++; else opening = direction;
                }
                if (blockedDirections != 3) continue;
                float clearance = float.PositiveInfinity;
                for (int oz = 0; oz < result.rows; oz++)
                for (int ox = 0; ox < result.columns; ox++)
                {
                    if (!result.occupancy[oz * result.columns + ox]) continue;
                    float cx = Mathf.Max(0f, Mathf.Abs(x - ox) - 0.5f) * result.cellSizeMeters;
                    float cz = Mathf.Max(0f, Mathf.Abs(z - oz) - 0.5f) * result.cellSizeMeters;
                    clearance = Mathf.Min(clearance, Mathf.Sqrt(cx * cx + cz * cz));
                }
                if (clearance <= result.interiorClearanceMeters) continue;
                result.hasAccessibleInterior = true;
                result.interiorClearanceMeters = clearance;
                result.interiorPointMetric = new Vector3(minX + (x + 0.5f) * result.cellSizeMeters,
                    bottom, minZ + (z + 0.5f) * result.cellSizeMeters);
                result.openingDirectionMetric = new Vector3(dx[opening], 0f, dz[opening]);
            }
        }
        private sealed class Run { internal int left, right, bottom, top; }
        private static Vector2 Normal(Vector2 edge) => new Vector2(-edge.y, edge.x);
        private static Vector2 XZ(Vector3 point) => new Vector2(point.x, point.z);
        private static bool Positive(float value) => value > 0.000001f && !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
