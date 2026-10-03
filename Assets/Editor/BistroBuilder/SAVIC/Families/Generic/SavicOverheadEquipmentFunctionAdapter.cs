using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicOverheadEquipmentFunctionAdapter : ISavicPlaceableFunctionAdapter
    {
        internal const string Mode = "PASSIVE_OVERHEAD_BODY";
        internal const string Version = "1.0.0";
        internal const string ContractPath = "Assets/Resources/BistroBuilder/Spatial/Contracts/BB_SpatialContract_Passive_Overhead.asset";
        public string IntegrationMode => Mode;
        public string Fingerprint(SavicManifest manifest) => Version + "|" + (manifest.overheadEquipment?.inputFingerprint ?? string.Empty);
        public void Apply(GameObject root, SavicManifest manifest)
        {
            if (root == null || !PlanMatches(manifest) || root.GetComponent<RestaurantPlaceableObject>() == null ||
                root.GetComponent<RestaurantPlaceableObject>().HasInstanceId)
                throw new InvalidOperationException("Overhead authoring requires a current verified plan and an unactivated placeable.");
            var plan = manifest.overheadEquipment;
            var contract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(ContractPath);
            Transform visual = root.transform.Find("Visual");
            if (visual == null || contract == null || !contract.ValidateDefinition(out _))
                throw new InvalidOperationException("The canonical overhead visual or passive spatial contract is missing.");
            visual.localScale = Vector3.one * plan.uniformScale;
            visual.localPosition = new Vector3(0f, plan.installationBottomMeters, 0f);
            visual.localRotation = Quaternion.identity;
            foreach (Collider sourceCollider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(sourceCollider);
            var collider = root.GetComponent<BoxCollider>();
            if (collider == null) collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, plan.installationBottomMeters + plan.finalSizeMeters.y * 0.5f, 0f);
            collider.size = plan.finalSizeMeters; collider.isTrigger = false; collider.enabled = true;
            root.GetComponent<RestaurantPlacementFootprint>().ConfigureRuntime(Vector3.zero,
                new Vector2(plan.finalSizeMeters.x, plan.finalSizeMeters.z), 0.05f);
            var proxy = root.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
            if (proxy == null) proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Simple); proxy.ClearParts();
            proxy.AddPart(new BistroBuilderSpatialProxyPart { partId = "passive.body", localCenter = collider.center,
                size = new Vector2(collider.size.x, collider.size.z), hasVerticalExtent = true, height = collider.size.y });
            if (root.GetComponent<BistroBuilderSpatialSubject>() == null) root.AddComponent<BistroBuilderSpatialSubject>();
            if (root.GetComponent<BistroBuilderSpatialPhysicalFootprintAdapter>() == null) root.AddComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
            var binding = root.GetComponent<BistroBuilderPassiveBodySpatialBinding>();
            if (binding == null) binding = root.AddComponent<BistroBuilderPassiveBodySpatialBinding>();
            binding.ConfigureForEditor(contract);
        }
        public bool Validate(GameObject root, SavicManifest manifest, out string error)
        {
            error = "Passive overhead body does not match its current installation profile.";
            if (root == null || !PlanMatches(manifest)) return false;
            var plan = manifest.overheadEquipment;
            var binding = root.GetComponent<BistroBuilderPassiveBodySpatialBinding>();
            var subject = root.GetComponent<BistroBuilderSpatialSubject>();
            var collider = root.GetComponent<BoxCollider>();
            Transform visual = root.transform.Find("Visual");
            if (binding == null || !binding.enabled || !binding.ValidateConfiguration(out _) ||
                subject == null || subject.Contract != AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(ContractPath) ||
                collider == null || !collider.enabled || collider.isTrigger || root.GetComponentsInChildren<Collider>(true).Length != 1 ||
                !Close(collider.size, plan.finalSizeMeters) ||
                !Close(collider.center, new Vector3(0f, plan.installationBottomMeters + plan.finalSizeMeters.y * 0.5f, 0f)) ||
                visual == null || !Close(visual.localScale, Vector3.one * plan.uniformScale) ||
                !Close(visual.localPosition, new Vector3(0f, plan.installationBottomMeters, 0f)) ||
                Quaternion.Angle(visual.localRotation, Quaternion.identity) > 0.001f ||
                root.GetComponent<RestaurantPlacementFootprint>().LocalCenter != Vector3.zero ||
                Math.Abs(root.GetComponent<RestaurantPlacementFootprint>().MinimumClearance - 0.05f) > 0.0001f ||
                subject.Proxy.Parts.Count != 1) return false;
            var part = subject.Proxy.Parts[0];
            if (part.anchor != null || !part.enabled || part.layer != BistroBuilderSpatialProxyLayer.Static ||
                part.shapeKind != BistroBuilderSpatialShapeKind.OrientedBox || !part.hasVerticalExtent ||
                Math.Abs(part.height - collider.size.y) > 0.0001f || !Close(part.localCenter, collider.center) ||
                Vector2.Distance(part.size, new Vector2(collider.size.x, collider.size.z)) > 0.0001f) return false;
            error = string.Empty; return true;
        }
        internal static bool PlanMatches(SavicManifest manifest)
        {
            var plan = manifest?.overheadEquipment;
            var profile = AssetDatabase.LoadAssetAtPath<SavicOverheadEquipmentProfile>(SavicOverheadEquipmentAuthoringPlanner.ProfilePath);
            return plan?.planned == true && profile != null && profile.IsValid &&
                manifest.classification?.type == "KitchenEquipment" && plan.plannerVersion == SavicOverheadEquipmentAuthoringPlanner.Version &&
                plan.profileVersion == profile.profileVersion && manifest.model3D?.hasUsableBounds == true &&
                Math.Abs(plan.installationBottomMeters - profile.installationBottomMeters) < 0.0001f &&
                Math.Abs(plan.uniformScale * manifest.model3D.widthMeters - profile.widthMeters) < 0.0001f &&
                Close(plan.finalSizeMeters, new Vector3(manifest.model3D.widthMeters, manifest.model3D.heightMeters,
                    manifest.model3D.depthMeters) * plan.uniformScale) &&
                plan.profileFingerprint == SavicHashService.ComputeSha256Text(JsonUtility.ToJson(profile)) &&
                plan.inputFingerprint == SavicOverheadEquipmentAuthoringPlanner.ComputeFingerprint(plan) &&
                plan.sourceHash == manifest.source?.sourceHash && plan.providerMetadataHash == manifest.source.providerMetadataHash;
        }
        private static bool Close(Vector3 first, Vector3 second) => Vector3.Distance(first, second) < 0.0001f;
    }
}
