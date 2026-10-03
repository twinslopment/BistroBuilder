using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicOverheadEquipmentAuthoringRecord
    {
        public bool planned;
        public string plannerVersion = string.Empty, profileVersion = string.Empty, profileFingerprint = string.Empty;
        public string sourceHash = string.Empty, providerMetadataHash = string.Empty, inputFingerprint = string.Empty;
        public float uniformScale, installationBottomMeters;
        public Vector3 finalSizeMeters;
        public string evidence = string.Empty;
    }
    internal static class SavicOverheadEquipmentAuthoringPlanner
    {
        internal const string Version = "1.0.0";
        internal const string ProfilePath = "Assets/Data/Restaurant/SAVIC/OverheadEquipmentProfile_Standard.asset";
        internal static SavicOverheadEquipmentProfile GetOrCreateProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<SavicOverheadEquipmentProfile>(ProfilePath);
            if (profile != null) return profile;
            if (AssetDatabase.LoadMainAssetAtPath(ProfilePath) != null) throw new InvalidOperationException("Overhead profile path is occupied.");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Restaurant/SAVIC")) AssetDatabase.CreateFolder("Assets/Data/Restaurant", "SAVIC");
            profile = ScriptableObject.CreateInstance<SavicOverheadEquipmentProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath); AssetDatabase.SaveAssets(); return profile;
        }
        internal static bool IsVerifiedHood(SavicManifest manifest, SavicStorageLayout layout = null)
        {
            if (manifest?.classification?.type != "KitchenEquipment" || manifest.classification.score < 0.8f ||
                manifest.source?.sourceKind != SavicSourceKind.Model3D.ToString()) return false;
            string semantic = SavicProviderMetadataService.ResolveSemanticName(manifest, layout);
            var decision = SavicEquipmentIntegrationPolicy.Resolve(semantic, "KitchenEquipment");
            return decision.IsPassive && semantic.IndexOf("hood", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        internal static bool TryPlan(SavicManifest manifest, GameObject source, SavicOverheadEquipmentProfile profile,
            out SavicOverheadEquipmentAuthoringRecord plan, out string error, SavicStorageLayout layout = null)
        {
            plan = new SavicOverheadEquipmentAuthoringRecord(); error = "Passive overhead identity, source or authored profile is unavailable.";
            if (source == null || profile == null || !profile.IsValid || !IsVerifiedHood(manifest, layout) ||
                string.IsNullOrWhiteSpace(manifest.source.sourceHash)) return false;
            var model = SavicModelAnalyzer.Analyze(source, SavicModelAnalysisMode.GenericStatic);
            var prior = manifest.model3D;
            if (!model.analyzed || !model.hasUsableBounds || model.hasSkinnedMeshes || prior == null ||
                !prior.analyzed || !prior.hasUsableBounds || prior.triangleCount != model.triangleCount ||
                Math.Abs(prior.widthMeters - model.widthMeters) > 0.001f || Math.Abs(prior.heightMeters - model.heightMeters) > 0.001f ||
                Math.Abs(prior.depthMeters - model.depthMeters) > 0.001f ||
                Vector3.Distance(new Vector3(prior.boundsCenterX, prior.boundsCenterY, prior.boundsCenterZ),
                    new Vector3(model.boundsCenterX, model.boundsCenterY, model.boundsCenterZ)) > 0.001f)
            { error = "Canonical source analysis is stale or unsuitable for static overhead authoring."; return false; }
            float scale = profile.widthMeters / model.widthMeters;
            Vector3 size = new Vector3(model.widthMeters, model.heightMeters, model.depthMeters) * scale;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f || size.y < 0.1f ||
                size.y > profile.maximumBodyHeightMeters || size.z < profile.minimumDepthMeters || size.z > profile.maximumDepthMeters)
            { error = "Uniform overhead normalization exceeds the authored physical envelope."; return false; }
            plan = new SavicOverheadEquipmentAuthoringRecord { planned = true, plannerVersion = Version,
                sourceHash = manifest.source.sourceHash, providerMetadataHash = manifest.source.providerMetadataHash,
                profileVersion = profile.profileVersion, profileFingerprint = SavicHashService.ComputeSha256Text(JsonUtility.ToJson(profile)),
                uniformScale = scale, finalSizeMeters = size, installationBottomMeters = profile.installationBottomMeters,
                evidence = "Verified passive kitchen-hood identity; uniform width and bottom installation height are authored by the common profile. No ceiling or extraction simulation inferred." };
            plan.inputFingerprint = ComputeFingerprint(plan); error = string.Empty; return true;
        }
        internal static string ComputeFingerprint(SavicOverheadEquipmentAuthoringRecord plan)
        {
            var copy = JsonUtility.FromJson<SavicOverheadEquipmentAuthoringRecord>(JsonUtility.ToJson(plan));
            copy.inputFingerprint = string.Empty; return SavicHashService.ComputeSha256Text(JsonUtility.ToJson(copy));
        }
    }
}
