using System;
using System.Globalization;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicFloorLampAuthoringPlanner
    {
        internal const string Version = "1.0.0";
        internal const string ProfilePath = "Assets/Data/Restaurant/SAVIC/FloorLampProfile_Standard.asset";

        internal static SavicFloorLampProfile GetOrCreateProfile()
        {
            SavicFloorLampProfile existing = AssetDatabase.LoadAssetAtPath<SavicFloorLampProfile>(ProfilePath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(ProfilePath) != null)
                throw new InvalidOperationException("Floor-lamp profile path is occupied by another asset type.");
            string directory = Path.GetDirectoryName(ProfilePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder("Assets/Data/Restaurant", "SAVIC");
            SavicFloorLampProfile profile = ScriptableObject.CreateInstance<SavicFloorLampProfile>();
            profile.name = "SAVIC Standard Floor Lamp";
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        internal static bool TryPlan(SavicManifest manifest, GameObject source,
            SavicFloorLampProfile profile, out SavicFloorLampAuthoringRecord plan, out string error,
            SavicStorageLayout layout = null)
        {
            plan = new SavicFloorLampAuthoringRecord();
            error = "Floor-lamp identity, profile or source geometry is not valid.";
            if (source == null || profile == null || !profile.IsValid || manifest?.source == null ||
                manifest.classification?.type != "FloorLamp" || manifest.classification.score < 0.8f ||
                manifest.source.sourceKind != SavicSourceKind.Model3D.ToString()) return false;
            string subject = SavicProviderMetadataService.ResolveSemanticName(manifest, layout);
            string[] words = subject.ToLowerInvariant().Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (Array.IndexOf(words, "floor") < 0 || Array.IndexOf(words, "lamp") < 0) return false;
            SavicModelAnalysisRecord analysis = manifest.model3D;
            if (analysis == null || !analysis.analyzed || !analysis.hasUsableBounds ||
                analysis.hasSkinnedMeshes || analysis.hasNegativeScale ||
                analysis.triangleCount <= 0 || analysis.triangleCount > 2000000 ||
                analysis.heightMeters < profile.minimumHeight || analysis.heightMeters > profile.maximumHeight ||
                analysis.widthMeters < profile.minimumWidth || analysis.widthMeters > profile.maximumWidth ||
                analysis.depthMeters < profile.minimumWidth || analysis.depthMeters > profile.maximumWidth) return false;

            BandBounds baseBand = new BandBounds();
            BandBounds stemBand = new BandBounds();
            BandBounds shadeBand = new BandBounds();
            float minY = analysis.boundsCenterY - analysis.heightMeters * 0.5f;
            int verticesRead = 0;
            foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                using Mesh.MeshDataArray dataArray = MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh);
                Mesh.MeshData data = dataArray[0];
                using NativeArray<Vector3> vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
                data.GetVertices(vertices);
                NativeArray<Vector3> transformed = vertices;
                Matrix4x4 metric = SavicMetricSpace.LocalToMetric(source.transform, filter.transform);
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 point = metric.MultiplyPoint3x4(vertices[index]);
                    if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z))
                    { error = "Floor-lamp mesh contains invalid vertices."; return false; }
                    transformed[index] = point;
                    verticesRead++;
                }
                for (int subMesh = 0; subMesh < data.subMeshCount; subMesh++)
                {
                    SubMeshDescriptor descriptor = data.GetSubMesh(subMesh);
                    if (descriptor.topology != MeshTopology.Triangles) continue;
                    using NativeArray<int> indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                    data.GetIndices(indices, subMesh, true);
                    for (int index = 0; index + 2 < indices.Length; index += 3)
                    {
                        int ia = indices[index], ib = indices[index + 1], ic = indices[index + 2];
                        if ((uint)ia >= vertices.Length || (uint)ib >= vertices.Length || (uint)ic >= vertices.Length)
                        { error = "Floor-lamp mesh contains invalid triangle indices."; return false; }
                        Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                        baseBand.IncludeTriangle(a, b, c, minY, minY + profile.baseTopRatio * analysis.heightMeters);
                        stemBand.IncludeTriangle(a, b, c, minY + profile.stemBottomRatio * analysis.heightMeters,
                            minY + profile.stemTopRatio * analysis.heightMeters);
                        shadeBand.IncludeTriangle(a, b, c, minY + profile.shadeBottomRatio * analysis.heightMeters,
                            minY + analysis.heightMeters);
                    }
                }
            }
            if (verticesRead == 0 || !baseBand.HasBounds || !stemBand.HasBounds || !shadeBand.HasBounds ||
                !SpanAtLeast(baseBand.Bounds, analysis, profile.minimumBaseSpanRatio) ||
                !SpanAtLeast(shadeBand.Bounds, analysis, profile.minimumShadeSpanRatio) ||
                stemBand.Bounds.size.x > analysis.widthMeters * profile.maximumStemSpanRatio ||
                stemBand.Bounds.size.z > analysis.depthMeters * profile.maximumStemSpanRatio)
            { error = "Measured geometry does not confirm a stable base, slender stem and broad upper shade."; return false; }

            Vector3 center = shadeBand.Bounds.center;
            Vector3 emitter = center - new Vector3(analysis.boundsCenterX, minY, analysis.boundsCenterZ);
            plan = new SavicFloorLampAuthoringRecord
            {
                planned = true, plannerVersion = Version, profileVersion = profile.profileVersion,
                profileFingerprint = SavicHashService.ComputeSha256Text(JsonUtility.ToJson(profile)),
                sourceHash = manifest.source.sourceHash, providerMetadataHash = manifest.source.providerMetadataHash,
                emitterLocalPosition = emitter, shadeCenter = center, shadeSize = shadeBand.Bounds.size,
                intensity = profile.intensity, rangeMeters = profile.rangeMeters,
                colorTemperatureKelvin = profile.colorTemperatureKelvin,
                evidence = "Verified floor-lamp subject; measured broad base and upper shade with a slender central stem. " +
                    "Emitter derived from upper shade bounds; intensity/range/temperature are authored profile settings. " +
                    "Source height=" + analysis.heightMeters.ToString("0.###", CultureInfo.InvariantCulture) + " m."
            };
            plan.inputFingerprint = ComputeFingerprint(plan);
            error = string.Empty;
            return true;
        }

        private static bool SpanAtLeast(Bounds bounds, SavicModelAnalysisRecord model, float ratio) =>
            bounds.size.x >= model.widthMeters * ratio && bounds.size.z >= model.depthMeters * ratio;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static string ComputeFingerprint(SavicFloorLampAuthoringRecord record)
        {
            SavicFloorLampAuthoringRecord clone = JsonUtility.FromJson<SavicFloorLampAuthoringRecord>(JsonUtility.ToJson(record));
            clone.inputFingerprint = string.Empty;
            return SavicHashService.ComputeSha256Text(JsonUtility.ToJson(clone));
        }

        private sealed class BandBounds
        {
            internal bool HasBounds;
            internal Bounds Bounds;
            internal void IncludeTriangle(Vector3 a, Vector3 b, Vector3 c, float bottom, float top)
            {
                IncludeClipped(a, bottom, top);
                IncludeClipped(b, bottom, top);
                IncludeClipped(c, bottom, top);
                IncludeEdge(a, b, bottom, top);
                IncludeEdge(b, c, bottom, top);
                IncludeEdge(c, a, bottom, top);
            }
            private void IncludeClipped(Vector3 vertex, float bottom, float top)
            { if (vertex.y >= bottom && vertex.y <= top) Include(vertex); }
            private void IncludeEdge(Vector3 a, Vector3 b, float bottom, float top)
            {
                if (Math.Abs(b.y - a.y) < 0.0000001f) return;
                float lower = (bottom - a.y) / (b.y - a.y);
                float upper = (top - a.y) / (b.y - a.y);
                if (lower >= 0 && lower <= 1) Include(Vector3.LerpUnclamped(a, b, lower));
                if (upper >= 0 && upper <= 1) Include(Vector3.LerpUnclamped(a, b, upper));
            }
            internal void Include(Vector3 vertex)
            {
                if (HasBounds) Bounds.Encapsulate(vertex);
                else { Bounds = new Bounds(vertex, Vector3.zero); HasBounds = true; }
            }
        }
    }
}
