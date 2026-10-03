using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicBarCounterFunctionAdapter : ISavicPlaceableFunctionAdapter
    {
        internal const string Mode = "NATIVE_BAR_COUNTER";
        internal const string Version = "1.1.0";
        internal const string BodyName = "SAVIC_BarPhysicalBody";
        internal const string SpotName = "SAVIC_BarServiceSpot";
        internal const string ContractPath = "Assets/Resources/BistroBuilder/Spatial/Contracts/BB_SpatialContract_Bar_Service_Spot.asset";
        public string IntegrationMode => Mode;
        public string Fingerprint(SavicManifest manifest) => Version + "|" + (manifest.barCounter?.inputFingerprint ?? string.Empty);

        public void Apply(GameObject root, SavicManifest manifest)
        {
            if (!PlanMatches(manifest) || root == null || root.GetComponent<RestaurantPlaceableObject>() == null ||
                root.GetComponent<RestaurantPlaceableObject>().HasInstanceId)
                throw new InvalidOperationException("Bar authoring requires a verified plan and an unactivated placeable root.");
            SavicBarCounterAuthoringRecord plan = manifest.barCounter;
            Transform visual = root.transform.Find("Visual");
            if (visual == null) throw new InvalidOperationException("Canonical bar visual root is missing.");
            BistroBuilderSpatialContractDefinition contract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(ContractPath);
            if (contract == null || !contract.ValidateDefinition(out _))
                throw new InvalidOperationException("Native bar spatial contract is unavailable.");
            BistroBuilderBarPlaceableBinding binding = root.GetComponent<BistroBuilderBarPlaceableBinding>();
            if (binding != null) binding.ReleaseRuntimeRegistration();
            visual.localScale = Vector3.one * plan.uniformScale;
            foreach (Collider collider in root.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            Transform physical = root.transform.Find(BodyName);
            if (physical != null) Object.DestroyImmediate(physical.gameObject);
            physical = MakeNode(root, BodyName);
            BistroBuilderAdaptiveSpatialProxy proxy = root.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
            if (proxy == null) proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Compound); proxy.ClearParts();
            for (int index = 0; index < plan.physicalBoxes.Count; index++)
            {
                SavicBarPhysicalBox box = plan.physicalBoxes[index];
                BoxCollider collider = MakeNode(physical.gameObject, "Body_" + index.ToString("D2")).gameObject.AddComponent<BoxCollider>();
                collider.center = box.center; collider.size = box.size; collider.isTrigger = false;
                proxy.AddPart(new BistroBuilderSpatialProxyPart {
                    partId = "bar.body." + index.ToString("D2"), localCenter = box.center,
                    size = new Vector2(box.size.x, box.size.z), layer = BistroBuilderSpatialProxyLayer.Static });
            }
            root.GetComponent<RestaurantPlacementFootprint>().ConfigureRuntime(Vector3.zero,
                new Vector2(plan.finalSizeMeters.x, plan.finalSizeMeters.z));
            Transform spotNode = root.transform.Find(SpotName);
            if (spotNode == null) spotNode = MakeNode(root, SpotName);
            Transform customer = spotNode.Find("Customer");
            if (customer == null) customer = MakeNode(spotNode.gameObject, "Customer");
            Transform waiter = spotNode.Find("Waiter");
            if (waiter == null) waiter = MakeNode(spotNode.gameObject, "Waiter");
            Transform surface = spotNode.Find("CounterSurface");
            if (surface == null) surface = MakeNode(spotNode.gameObject, "CounterSurface");
            spotNode.localPosition = Vector3.zero; spotNode.localRotation = Quaternion.identity; spotNode.localScale = Vector3.one;
            customer.localPosition = plan.customerLocalPosition; customer.localRotation = Quaternion.LookRotation(plan.openingDirection);
            waiter.localPosition = plan.waiterLocalPosition; waiter.localRotation = Quaternion.LookRotation(-plan.openingDirection);
            surface.localPosition = new Vector3(plan.customerLocalPosition.x, plan.counterHeightMeters,
                plan.customerLocalPosition.z);
            surface.localRotation = Quaternion.identity; surface.localScale = Vector3.one;
            BistroBuilderBarServiceSpot spot = spotNode.GetComponent<BistroBuilderBarServiceSpot>();
            if (spot == null) spot = spotNode.gameObject.AddComponent<BistroBuilderBarServiceSpot>();
            if (!spot.TryConfigure("bar.template.slot_00", customer, waiter, 1, true))
                throw new InvalidOperationException("Native bar service spot authoring was rejected.");
            if (!spot.TryConfigureCounterSurface(surface))
                throw new InvalidOperationException("Native bar counter surface authoring was rejected.");
            if (binding == null) binding = root.AddComponent<BistroBuilderBarPlaceableBinding>();
            binding.ConfigureForEditor(new[] { spot }, contract);
            BistroBuilderBarBodySpatialAdapter body = root.GetComponent<BistroBuilderBarBodySpatialAdapter>();
            if (body == null) body = root.AddComponent<BistroBuilderBarBodySpatialAdapter>();
            body.ConfigureForEditor(binding, contract);
            if (!Validate(root, manifest, out string error)) throw new InvalidOperationException(error);
        }

        public bool Validate(GameObject root, SavicManifest manifest, out string error)
        {
            error = "Bar prefab differs from its verified physical/function plan.";
            if (!PlanMatches(manifest) || root == null) return false;
            SavicBarCounterAuthoringRecord plan = manifest.barCounter;
            Transform visual = root.transform.Find("Visual"), physical = root.transform.Find(BodyName);
            BistroBuilderBarBodySpatialAdapter body = root.GetComponent<BistroBuilderBarBodySpatialAdapter>();
            BistroBuilderBarPlaceableBinding binding = root.GetComponent<BistroBuilderBarPlaceableBinding>();
            BistroBuilderAdaptiveSpatialProxy proxy = root.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
            if (visual == null || physical == null || body == null || binding == null || proxy == null ||
                !Close(visual.localScale, Vector3.one * plan.uniformScale) || !IdentityPose(visual) ||
                !IdentityPose(physical) || !Close(physical.localScale, Vector3.one) ||
                body.GetComponent<BistroBuilderSpatialSubject>().Contract != AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(ContractPath) ||
                root.GetComponents<Collider>().Length != 0 ||
                visual.GetComponentsInChildren<Collider>(true).Length != 0 || binding.Spots.Count != 1 ||
                root.GetComponentsInChildren<BistroBuilderBarServiceSpot>(true).Length != 1 ||
                !body.ValidateConfiguration(out error)) return false;
            BoxCollider[] colliders = physical.GetComponentsInChildren<BoxCollider>(true);
            if (colliders.Length != plan.physicalBoxes.Count || root.GetComponentsInChildren<Collider>(true).Length != colliders.Length ||
                proxy.Parts.Count != plan.physicalBoxes.Count) return false;
            for (int index = 0; index < colliders.Length; index++)
            {
                SavicBarPhysicalBox expected = plan.physicalBoxes[index];
                BistroBuilderSpatialProxyPart part = proxy.Parts[index];
                if (!Close(colliders[index].center, expected.center) || !Close(colliders[index].size, expected.size) ||
                    colliders[index].isTrigger || !colliders[index].enabled ||
                    !Close(colliders[index].transform.localPosition, Vector3.zero) ||
                    !Close(colliders[index].transform.localScale, Vector3.one) ||
                    Quaternion.Angle(colliders[index].transform.localRotation, Quaternion.identity) > 0.001f ||
                    part.anchor != null || part.layer != BistroBuilderSpatialProxyLayer.Static || !part.enabled ||
                    part.shapeKind != BistroBuilderSpatialShapeKind.OrientedBox ||
                    !Close(part.localCenter, expected.center) ||
                    Vector2.Distance(part.size, new Vector2(expected.size.x, expected.size.z)) > 0.0001f) return false;
            }
            BistroBuilderBarServiceSpot spot = binding.Spots[0];
            Vector3 customer = root.transform.InverseTransformPoint(spot.CustomerPoint.position);
            Vector3 waiter = root.transform.InverseTransformPoint(spot.WaiterServicePoint.position);
            Transform surface = spot.CounterSurfacePoint;
            if (!Close(customer, plan.customerLocalPosition) || !Close(waiter, plan.waiterLocalPosition) ||
                surface == null || !surface.IsChildOf(spot.transform) ||
                !Close(root.transform.InverseTransformPoint(surface.position),
                    new Vector3(plan.customerLocalPosition.x, plan.counterHeightMeters, plan.customerLocalPosition.z)) ||
                spot.Capacity != 1 || !spot.AllowsStandingService ||
                Vector3.Angle(root.transform.InverseTransformDirection(spot.CustomerPoint.forward), plan.openingDirection) > 0.01f ||
                Vector3.Angle(root.transform.InverseTransformDirection(spot.WaiterServicePoint.forward), -plan.openingDirection) > 0.01f) return false;
            List<RestaurantPlacementShape> shapes = new List<RestaurantPlacementShape>();
            if (!BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(root.GetComponent<RestaurantPlacementFootprint>(),
                    root.transform.position, root.transform.rotation, shapes, out error) || shapes.Count != colliders.Length) return false;
            error = string.Empty;
            return true;
        }

        internal static bool PlanMatches(SavicManifest manifest)
        {
            SavicBarCounterAuthoringRecord plan = manifest?.barCounter;
            SavicBarCounterProfile profile = AssetDatabase.LoadAssetAtPath<SavicBarCounterProfile>(SavicBarCounterAuthoringPlanner.ProfilePath);
            return plan?.planned == true && manifest.classification?.type == "BarCounter" &&
                profile != null && profile.IsValid &&
                plan.profileFingerprint == SavicHashService.ComputeSha256Text(JsonUtility.ToJson(profile)) &&
                plan.plannerVersion == SavicBarCounterAuthoringPlanner.Version && plan.geometryVersion == SavicCompoundBodyGeometryAnalyzer.Version &&
                plan.inputFingerprint == SavicBarCounterAuthoringPlanner.ComputeFingerprint(plan) &&
                plan.sourceHash == manifest.source?.sourceHash && plan.providerMetadataHash == manifest.source.providerMetadataHash &&
                plan.physicalBoxes != null && plan.physicalBoxes.Count > 0 &&
                plan.physicalBoxes.Count <= BistroBuilderSpatialPhysicalFootprintAdapter.MaximumStaticParts;
        }
        private static Transform MakeNode(GameObject parent, string name)
        {
            GameObject child = new GameObject(name);
            SceneManager.MoveGameObjectToScene(child, parent.scene); child.transform.SetParent(parent.transform, false);
            return child.transform;
        }
        private static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.0001f;
        private static bool IdentityPose(Transform node) => Close(node.localPosition, Vector3.zero) &&
            Quaternion.Angle(node.localRotation, Quaternion.identity) < 0.001f;
    }
}
