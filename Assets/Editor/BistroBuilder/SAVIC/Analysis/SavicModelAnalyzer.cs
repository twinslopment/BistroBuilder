using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicModelAnalyzer
    {
        internal const string Version = "1.0.0";
        private const float MinimumDimension = 0.0001f;

        internal static SavicModelAnalysisRecord Analyze(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            MeshFilter[] meshFilters =
                root.GetComponentsInChildren<MeshFilter>(true);
            SkinnedMeshRenderer[] skinnedRenderers =
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);

            HashSet<Mesh> uniqueMeshes = new HashSet<Mesh>();
            HashSet<Material> uniqueMaterials = new HashSet<Material>();

            long vertexCount = 0;
            long triangleCount = 0;
            int meshInstanceCount = 0;
            int materialSlotCount = 0;
            int missingMaterialSlots = 0;
            bool hasNegativeScale = false;

            BoundsAccumulator bounds =
                new BoundsAccumulator(root.transform);

            for (int index = 0; index < meshFilters.Length; index++)
            {
                MeshFilter filter = meshFilters[index];
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;

                meshInstanceCount++;
                bounds.Include(filter.transform, mesh.bounds);

                if (uniqueMeshes.Add(mesh))
                {
                    vertexCount += Math.Max(0, mesh.vertexCount);
                    triangleCount += CountTriangles(mesh);
                }
            }

            for (int index = 0; index < skinnedRenderers.Length; index++)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[index];
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null)
                    continue;

                meshInstanceCount++;
                bounds.Include(renderer.transform, renderer.localBounds);

                if (uniqueMeshes.Add(mesh))
                {
                    vertexCount += Math.Max(0, mesh.vertexCount);
                    triangleCount += CountTriangles(mesh);
                }
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                Material[] materials = renderer.sharedMaterials;

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    materialSlotCount++;
                    Material material = materials[materialIndex];

                    if (material == null)
                    {
                        missingMaterialSlots++;
                        continue;
                    }

                    uniqueMaterials.Add(material);
                }
            }

            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);

            for (int index = 0; index < transforms.Length; index++)
            {
                Vector3 scale = transforms[index].localScale;
                if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
                {
                    hasNegativeScale = true;
                    break;
                }
            }

            Vector3 center = bounds.HasBounds
                ? bounds.Bounds.center
                : Vector3.zero;

            Vector3 size = bounds.HasBounds
                ? bounds.Bounds.size
                : Vector3.zero;

            bool hasUsableBounds =
                bounds.HasBounds &&
                IsFinite(center) &&
                IsFinite(size) &&
                size.x >= MinimumDimension &&
                size.y >= MinimumDimension &&
                size.z >= MinimumDimension;

            return new SavicModelAnalysisRecord
            {
                analyzed = true,
                analyzerVersion = Version,
                hasUsableBounds = hasUsableBounds,
                boundsCenterX = center.x,
                boundsCenterY = center.y,
                boundsCenterZ = center.z,
                widthMeters = size.x,
                heightMeters = size.y,
                depthMeters = size.z,
                rendererCount = renderers.Length,
                meshInstanceCount = meshInstanceCount,
                uniqueMeshCount = uniqueMeshes.Count,
                vertexCount = vertexCount,
                triangleCount = triangleCount,
                materialSlotCount = materialSlotCount,
                uniqueMaterialCount = uniqueMaterials.Count,
                missingMaterialSlots = missingMaterialSlots,
                hasSkinnedMeshes = skinnedRenderers.Length > 0,
                hasNegativeScale = hasNegativeScale,
                analyzedUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static long CountTriangles(Mesh mesh)
        {
            long triangleCount = 0;

            for (int subMeshIndex = 0;
                 subMeshIndex < mesh.subMeshCount;
                 subMeshIndex++)
            {
                MeshTopology topology =
                    mesh.GetTopology(subMeshIndex);

                if (topology != MeshTopology.Triangles)
                    continue;

                ulong indexCount =
                    mesh.GetIndexCount(subMeshIndex);

                ulong subMeshTriangles =
                    indexCount / 3UL;

                triangleCount =
                    subMeshTriangles > (ulong)(long.MaxValue - triangleCount)
                        ? long.MaxValue
                        : triangleCount + (long)subMeshTriangles;
            }

            return triangleCount;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private sealed class BoundsAccumulator
        {
            private readonly Matrix4x4 rootWorldToLocal;
            private Bounds bounds;

            internal BoundsAccumulator(Transform root)
            {
                rootWorldToLocal = root.worldToLocalMatrix;
            }

            internal bool HasBounds { get; private set; }
            internal Bounds Bounds => bounds;

            internal void Include(
                Transform owner,
                Bounds localBounds)
            {
                Matrix4x4 toRoot =
                    rootWorldToLocal *
                    owner.localToWorldMatrix;

                Vector3 min = localBounds.min;
                Vector3 max = localBounds.max;

                IncludePoint(toRoot.MultiplyPoint3x4(
                    new Vector3(min.x, min.y, min.z)));
                IncludePoint(toRoot.MultiplyPoint3x4(
                    new Vector3(max.x, min.y, min.z)));
                IncludePoint(toRoot.MultiplyPoint3x4(
                    new Vector3(min.x, max.y, min.z)));
                IncludePoint(toRoot.MultiplyPoint3x4(
                    new Vector3(max.x, max.y, min.z)));
                IncludePoint(toRoot.MultiplyPoint3x4(
                    new Vector3(min.x, min.y, max.z)));
                IncludePoint(toRoot.MultiplyPoint3x4(
                    new Vector3(max.x, min.y, max.z)));
                IncludePoint(toRoot.MultiplyPoint3x4(
                    new Vector3(min.x, max.y, max.z)));
                IncludePoint(toRoot.MultiplyPoint3x4(
                    new Vector3(max.x, max.y, max.z)));
            }

            private void IncludePoint(Vector3 point)
            {
                if (!IsFinite(point))
                    return;

                if (!HasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    HasBounds = true;
                    return;
                }

                bounds.Encapsulate(point);
            }
        }
    }
}
