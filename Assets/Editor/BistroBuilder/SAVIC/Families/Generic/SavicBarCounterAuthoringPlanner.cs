using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicBarCounterAuthoringRecord
    {
        public bool planned;
        public string plannerVersion = string.Empty;
        public string geometryVersion = string.Empty;
        public string profileVersion = string.Empty;
        public string profileFingerprint = string.Empty;
        public string sourceHash = string.Empty;
        public string providerMetadataHash = string.Empty;
        public string inputFingerprint = string.Empty;
        public float uniformScale;
        public float sourceCounterHeightMeters;
        public float counterHeightMeters;
        public float upperSurfaceCoverage;
        public Vector3 finalSizeMeters;
        public Vector3 waiterLocalPosition;
        public Vector3 customerLocalPosition;
        public Vector3 openingDirection;
        public float waiterClearanceMeters;
        public List<SavicBarPhysicalBox> physicalBoxes = new List<SavicBarPhysicalBox>();
        public string evidence = string.Empty;
    }

    [Serializable]
    internal sealed class SavicBarPhysicalBox
    {
        public Vector3 center;
        public Vector3 size;
    }

    internal static class SavicBarCounterAuthoringPlanner
    {
        internal const string Version = "1.0.0";
        internal const string ProfilePath = "Assets/Data/Restaurant/SAVIC/BarCounterProfile_Standard.asset";

        internal static SavicBarCounterProfile GetOrCreateProfile()
        {
            SavicBarCounterProfile existing = AssetDatabase.LoadAssetAtPath<SavicBarCounterProfile>(ProfilePath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(ProfilePath) != null)
                throw new InvalidOperationException("Bar-counter profile path is occupied by another asset type.");
            if (!AssetDatabase.IsValidFolder("Assets/Data/Restaurant/SAVIC"))
                AssetDatabase.CreateFolder("Assets/Data/Restaurant", "SAVIC");
            SavicBarCounterProfile profile = ScriptableObject.CreateInstance<SavicBarCounterProfile>();
            profile.name = "SAVIC Standard Bar Counter";
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        internal static bool TryPlan(SavicManifest manifest, GameObject source, SavicBarCounterProfile profile,
            out SavicBarCounterAuthoringRecord plan, out string error, SavicStorageLayout layout = null)
        {
            plan = new SavicBarCounterAuthoringRecord();
            error = "Bar-counter identity, profile or source is invalid.";
            if (source == null || profile == null || !profile.IsValid || manifest?.source == null ||
                string.IsNullOrEmpty(manifest.source.sourceHash) || manifest.classification?.type != "BarCounter" ||
                manifest.classification.score < 0.8f || manifest.source.sourceKind != SavicSourceKind.Model3D.ToString()) return false;
            string name = SavicProviderMetadataService.ResolveSemanticName(manifest, layout).Replace('_', ' ');
            if (!Regex.IsMatch(name, @"\bbar\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(name, @"\b(stool|chair|lamp|hood)\b", RegexOptions.IgnoreCase)) return false;
            // Recompute source geometry; diagnostic logs and previous authoring are not input authorities.
            SavicModelAnalysisRecord model = SavicModelAnalyzer.Analyze(source, SavicModelAnalysisMode.GenericStatic);
            if (!SameGeometry(manifest.model3D, model))
            { error = "Manifest geometry differs from the current source; canonical analysis must run first."; return false; }
            SavicCompoundBodyGeometryRecord body = SavicCompoundBodyGeometryAnalyzer.Analyze(source, model);
            if (!body.usable || !body.hasAccessibleInterior || !body.hasBroadUpperSurface ||
                body.upperSurfaceCoverage < profile.minimumUpperSurfaceCoverage)
            { error = "Source does not prove a broad upper counter with an accessible concave service body."; return false; }
            float scale = profile.counterHeightMeters / body.upperSurfaceHeightMeters;
            Vector3 size = new Vector3(model.widthMeters, model.heightMeters, model.depthMeters) * scale;
            float clearance = body.interiorClearanceMeters * scale;
            if (!FinitePositive(scale) || !FinitePositive(size.x) || !FinitePositive(size.y) || !FinitePositive(size.z) ||
                size.x < profile.minimumWidthMeters || size.x > profile.maximumWidthMeters ||
                size.z < profile.minimumDepthMeters || size.z > profile.maximumDepthMeters ||
                size.y < profile.counterHeightMeters || size.y > profile.maximumTotalHeightMeters ||
                clearance < profile.minimumWaiterClearanceMeters)
            { error = "Uniform normalization falls outside the physical profile or leaves insufficient waiter clearance."; return false; }
            Vector3 sourceOrigin = new Vector3(model.boundsCenterX,
                model.boundsCenterY - model.heightMeters * 0.5f, model.boundsCenterZ);
            Vector3 waiter = (body.interiorPointMetric - sourceOrigin) * scale;
            Vector3 direction = body.openingDirectionMetric;
            Vector3 customer = waiter;
            // The customer faces the closed counter side opposite the measured waiter entrance.
            if (Mathf.Abs(direction.x) > 0.5f)
                customer.x = -direction.x * (size.x * 0.5f + profile.customerOffsetMeters);
            else customer.z = -direction.z * (size.z * 0.5f + profile.customerOffsetMeters);
            plan = new SavicBarCounterAuthoringRecord {
                planned = true, plannerVersion = Version, geometryVersion = body.analyzerVersion,
                profileVersion = profile.profileVersion,
                profileFingerprint = SavicHashService.ComputeSha256Text(JsonUtility.ToJson(profile)),
                sourceHash = manifest.source.sourceHash, providerMetadataHash = manifest.source.providerMetadataHash,
                uniformScale = scale, sourceCounterHeightMeters = body.upperSurfaceHeightMeters,
                counterHeightMeters = profile.counterHeightMeters, upperSurfaceCoverage = body.upperSurfaceCoverage,
                finalSizeMeters = size, waiterLocalPosition = waiter, customerLocalPosition = customer,
                openingDirection = direction, waiterClearanceMeters = clearance,
                evidence = "Source triangles prove the highest broad upper surface and open compound body. " +
                    "Counter height, limits and port separation are authored profile settings; uniform source scale preserved. " +
                    "Geometry/ports do not grant publication or prove runtime service/persistence."
            };
            foreach (SavicCompoundBodyBox box in body.boxes)
                plan.physicalBoxes.Add(new SavicBarPhysicalBox {
                    center = new Vector3((box.centerMetric.x - sourceOrigin.x) * scale, size.y * 0.5f,
                        (box.centerMetric.z - sourceOrigin.z) * scale),
                    size = new Vector3(box.sizeMetric.x * scale, size.y, box.sizeMetric.y * scale) });
            if (!PortClear(plan, waiter, profile.minimumWaiterClearanceMeters) ||
                !PortClear(plan, customer, 0.3f))
            { plan = new SavicBarCounterAuthoringRecord(); error = "Authored ports overlap the conservative physical body."; return false; }
            plan.inputFingerprint = ComputeFingerprint(plan);
            error = string.Empty;
            return true;
        }

        internal static string ComputeFingerprint(SavicBarCounterAuthoringRecord record)
        {
            SavicBarCounterAuthoringRecord clone = JsonUtility.FromJson<SavicBarCounterAuthoringRecord>(JsonUtility.ToJson(record));
            clone.inputFingerprint = string.Empty;
            return SavicHashService.ComputeSha256Text(JsonUtility.ToJson(clone));
        }

        private static bool PortClear(SavicBarCounterAuthoringRecord plan, Vector3 point, float radius)
        {
            foreach (SavicBarPhysicalBox box in plan.physicalBoxes)
            {
                float x = Mathf.Max(0, Mathf.Abs(point.x - box.center.x) - box.size.x * 0.5f);
                float z = Mathf.Max(0, Mathf.Abs(point.z - box.center.z) - box.size.z * 0.5f);
                if (x * x + z * z <= radius * radius) return false;
            }
            return true;
        }
        private static bool SameGeometry(SavicModelAnalysisRecord left, SavicModelAnalysisRecord right) =>
            left != null && right != null && left.analyzed && right.analyzed && right.hasUsableBounds &&
            !right.hasSkinnedMeshes && !right.hasNegativeScale && left.triangleCount == right.triangleCount &&
            Close(left.widthMeters, right.widthMeters) && Close(left.heightMeters, right.heightMeters) &&
            Close(left.depthMeters, right.depthMeters) && Close(left.boundsCenterX, right.boundsCenterX) &&
            Close(left.boundsCenterY, right.boundsCenterY) && Close(left.boundsCenterZ, right.boundsCenterZ);
        private static bool Close(float a, float b) => !float.IsNaN(a) && !float.IsInfinity(a) && Mathf.Abs(a - b) < 0.00001f;
        private static bool FinitePositive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
