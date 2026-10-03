using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicBarStoolAuthoringRecord
    {
        public bool planned;
        public string plannerVersion = string.Empty, geometryVersion = string.Empty, profileVersion = string.Empty;
        public string profileFingerprint = string.Empty, sourceHash = string.Empty, providerMetadataHash = string.Empty;
        public string inputFingerprint = string.Empty, facingEvidence = string.Empty;
        public float sourceSeatHeightMeters, seatHeightMeters, counterHeightMeters, uniformScale, visualYawDegrees;
        public bool hasBackrest;
        public Vector3 finalSizeMeters, seatLocalPosition, sourceFacingMetric;
        public Vector3 approachLocalPosition;
        public float approachRadiusMeters, spotPositionToleranceMeters, maximumFacingAngleDegrees;
    }

    internal static class SavicBarStoolAuthoringPlanner
    {
        internal const string Version = "1.1.0";
        internal const string ProfilePath = "Assets/Data/Restaurant/SAVIC/BarStoolProfile_Standard.asset";
        internal static SavicBarStoolProfile GetOrCreateProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SavicBarStoolProfile>(ProfilePath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(ProfilePath) != null)
                throw new InvalidOperationException("Bar stool profile path is occupied by another asset type.");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Restaurant/SAVIC")) AssetDatabase.CreateFolder("Assets/Data/Restaurant", "SAVIC");
            var profile = ScriptableObject.CreateInstance<SavicBarStoolProfile>();
            profile.name = "SAVIC Standard Bar Stool";
            AssetDatabase.CreateAsset(profile, ProfilePath); AssetDatabase.SaveAssets(); return profile;
        }

        internal static bool TryPlan(SavicManifest manifest, GameObject source, SavicBarStoolProfile profile,
            out SavicBarStoolAuthoringRecord plan, out string error)
        {
            plan = new SavicBarStoolAuthoringRecord(); error = "Bar stool identity, source or physical profile is invalid.";
            if (manifest?.source == null || source == null || profile == null || !profile.IsValid ||
                manifest.classification?.type != "BarStool" || manifest.classification.score < 0.8f ||
                manifest.source.sourceKind != SavicSourceKind.Model3D.ToString() || string.IsNullOrEmpty(manifest.source.sourceHash)) return false;
            var model = SavicModelAnalyzer.Analyze(source, SavicModelAnalysisMode.GenericStatic);
            var previous = manifest.model3D;
            if (previous == null || !previous.analyzed || !previous.hasUsableBounds || previous.triangleCount != model.triangleCount ||
                Math.Abs(previous.widthMeters - model.widthMeters) > 0.001f || Math.Abs(previous.heightMeters - model.heightMeters) > 0.001f ||
                Math.Abs(previous.depthMeters - model.depthMeters) > 0.001f ||
                Vector3.Distance(new Vector3(previous.boundsCenterX, previous.boundsCenterY, previous.boundsCenterZ),
                    new Vector3(model.boundsCenterX, model.boundsCenterY, model.boundsCenterZ)) > 0.001f)
            { error = "Source geometry changed; canonical analysis must run before stool planning."; return false; }
            var geometry = SavicBarStoolGeometryAnalyzer.Analyze(source, model);
            if (!geometry.usable || !geometry.facingUsable)
            { error = "Source does not prove seating geometry and a safe front: " + geometry.facingEvidence; return false; }
            float scale = profile.seatHeightMeters / geometry.seatHeightMeters;
            float yaw = -Mathf.Atan2(geometry.facingMetric.x, geometry.facingMetric.z) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);
            Vector3 size = new Vector3(model.widthMeters, model.heightMeters, model.depthMeters) * scale;
            float cosine = Mathf.Abs(Mathf.Cos(yaw * Mathf.Deg2Rad)), sine = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad));
            Vector3 bounds = new Vector3(size.x * cosine + size.z * sine, size.y, size.x * sine + size.z * cosine);
            Vector3 seatSize = geometry.seatSizeMetric * scale;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0 ||
                seatSize.x < profile.minimumSeatSpanMeters || seatSize.z < profile.minimumSeatSpanMeters ||
                seatSize.x > profile.maximumSeatSpanMeters || seatSize.z > profile.maximumSeatSpanMeters ||
                bounds.x > profile.maximumBodySpanMeters || bounds.z > profile.maximumBodySpanMeters ||
                bounds.y < profile.seatHeightMeters || bounds.y > profile.maximumTotalHeightMeters)
            { error = "Uniform normalization exceeds the authored seat/body physical limits."; return false; }
            Vector3 origin = new Vector3(model.boundsCenterX, model.boundsCenterY - model.heightMeters * 0.5f, model.boundsCenterZ);
            plan = new SavicBarStoolAuthoringRecord {
                planned = true, plannerVersion = Version, geometryVersion = geometry.analyzerVersion, profileVersion = profile.profileVersion,
                profileFingerprint = SavicHashService.ComputeSha256Text(JsonUtility.ToJson(profile)),
                sourceHash = manifest.source.sourceHash, providerMetadataHash = manifest.source.providerMetadataHash,
                sourceSeatHeightMeters = geometry.seatHeightMeters, seatHeightMeters = profile.seatHeightMeters,
                counterHeightMeters = profile.counterHeightMeters, uniformScale = scale, visualYawDegrees = yaw,
                hasBackrest = geometry.hasBackrest, finalSizeMeters = bounds, seatLocalPosition = rotation * ((geometry.seatCenterMetric - origin) * scale),
                approachLocalPosition = new Vector3(0, 0, -bounds.z * 0.5f - profile.customerApproachRadiusMeters - profile.customerApproachMarginMeters),
                approachRadiusMeters = profile.customerApproachRadiusMeters, spotPositionToleranceMeters = profile.spotPositionToleranceMeters,
                maximumFacingAngleDegrees = profile.maximumFacingAngleDegrees,
                sourceFacingMetric = geometry.facingMetric, facingEvidence = geometry.facingEvidence };
            plan.inputFingerprint = ComputeFingerprint(plan); error = string.Empty; return true;
        }

        internal static string ComputeFingerprint(SavicBarStoolAuthoringRecord plan)
        {
            var copy = JsonUtility.FromJson<SavicBarStoolAuthoringRecord>(JsonUtility.ToJson(plan));
            copy.inputFingerprint = string.Empty;
            return SavicHashService.ComputeSha256Text(JsonUtility.ToJson(copy));
        }
    }
}
