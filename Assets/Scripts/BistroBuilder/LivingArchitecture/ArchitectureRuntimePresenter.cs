using System;
using System.Collections.Generic;
using BistroBuilder.LivingArchitecture.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.LivingArchitecture.Runtime
{
    /// <summary>
    /// Proyección descartable del estado arquitectónico canónico a GameObjects/Meshes de Unity.
    /// Nunca escribe de vuelta al dominio ni utiliza la escena como fuente de verdad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchitectureRuntimePresenter : MonoBehaviour
    {
        [SerializeField] private Material wallMaterial;
        [SerializeField] private bool addMeshColliders;

        private readonly Dictionary<string, GameObject> wallObjects = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private string projectedFingerprint = string.Empty;
        private Material feedbackOverrideMaterial;

        public string ProjectedFingerprint => projectedFingerprint;
        public int ProjectedWallCount => wallObjects.Count;

        /// <summary>
        /// Reconstruye de forma idempotente la proyección visual desde datos canónicos.
        /// Una reconstrucción nunca debe mutar building.
        /// </summary>
        public void Rebuild(ArchitectureBuilding building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            var before = ComputeFingerprint(building);
            ClearProjection();

            var levels = new List<ArchitectureLevel>(building.Levels ?? new List<ArchitectureLevel>());
            levels.Sort((a, b) => string.CompareOrdinal(a?.Id.Value, b?.Id.Value));
            foreach (var level in levels)
            {
                if (level == null) continue;
                var walls = new List<ArchitectureWall>(level.Walls ?? new List<ArchitectureWall>());
                walls.Sort((a, b) => string.CompareOrdinal(a?.Id.Value, b?.Id.Value));
                foreach (var wall in walls)
                {
                    if (wall == null) continue;
                    CreateWallObject(level, wall);
                }
            }

            var after = ComputeFingerprint(building);
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                ClearProjection();
                throw new InvalidOperationException("LA7_RUNTIME_MUTATED_CANONICAL_ARCHITECTURE");
            }

            projectedFingerprint = before;
        }

        /// <summary>
        /// LA10: aplica un material puramente presentacional a la proyección visible.
        /// No altera datos arquitectónicos ni materiales canónicos.
        /// </summary>
        public void SetFeedbackMaterial(Material material)
        {
            feedbackOverrideMaterial = material;
            foreach (var go in wallObjects.Values)
            {
                if (go == null) continue;
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = feedbackOverrideMaterial != null ? feedbackOverrideMaterial : wallMaterial;
            }
        }

        public void ClearFeedbackMaterial()
        {
            SetFeedbackMaterial(null);
        }

        public void ClearProjection()
        {
            var values = new List<GameObject>(wallObjects.Values);
            wallObjects.Clear();
            foreach (var go in values)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go);
            }
            projectedFingerprint = string.Empty;
        }

        private void CreateWallObject(ArchitectureLevel level, ArchitectureWall wall)
        {
            var data = ArchitectureWallMesher.Build(level, wall);
            var go = new GameObject("LA_Wall_" + wall.Id.Value);
            go.transform.SetParent(transform, false);

            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            var material = feedbackOverrideMaterial != null ? feedbackOverrideMaterial : wallMaterial;
            if (material != null) renderer.sharedMaterial = material;

            var mesh = BuildUnityMesh(data, wall.Id.Value);
            filter.sharedMesh = mesh;
            if (addMeshColliders)
            {
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }

            wallObjects.Add(wall.Id.Value, go);
        }

        private static Mesh BuildUnityMesh(ArchitectureMeshData data, string wallId)
        {
            var vertices = new Vector3[data.Vertices.Count];
            for (var i = 0; i < vertices.Length; i++)
            {
                var v = data.Vertices[i];
                vertices[i] = new Vector3((float)v.X, (float)v.Y, (float)v.Z);
            }

            var mesh = new Mesh { name = "LA_Mesh_" + wallId };
            if (vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = data.Triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static string ComputeFingerprint(ArchitectureBuilding building)
        {
            return new ArchitectureSnapshot { Building = building }.ComputeFingerprint();
        }

        private void OnDestroy()
        {
            wallObjects.Clear();
        }
    }
}
