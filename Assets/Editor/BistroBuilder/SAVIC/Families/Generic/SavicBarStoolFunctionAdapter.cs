using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    /// <summary>Physical native seat authoring. Does not associate, allocate, publish or certify runtime use.</summary>
    internal sealed class SavicBarStoolFunctionAdapter : ISavicPlaceableFunctionAdapter
    {
        internal const string Mode = "NATIVE_BAR_SEAT";
        internal const string Version = "1.1.0";
        internal const string ContractPath = "Assets/Resources/BistroBuilder/Spatial/Contracts/BB_SpatialContract_Seat_Bar_Stool.asset";
        internal const string SeatPort = "bar.seat";
        internal const string ApproachPort = "bar.seat.approach";
        public string IntegrationMode => Mode;
        public string Fingerprint(SavicManifest manifest) => Version + ":" + (manifest.barStool?.inputFingerprint ?? "");

        internal static BistroBuilderSpatialContractDefinition GetOrCreateContract()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(ContractPath);
            if (existing != null) return existing;
            if (AssetDatabase.LoadMainAssetAtPath(ContractPath) != null) throw new InvalidOperationException("Bar seat contract path is occupied.");
            var contract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
            contract.name = "BB Spatial Contract Bar Stool";
            contract.ConfigureForEditor("seat.bar.stool.standard", BistroBuilderBarSeatBinding.SpatialFamilyId,
                BistroBuilderAdaptiveSpatialProxyMode.Simple, new [] { "seating.bar", "seat.static", "service.bar" });
            contract.AddPortForEditor(new BistroBuilderSpatialPortDefinition { portId = SeatPort,
                kind = BistroBuilderSpatialPortKind.SeatBay, radius = 0.32f });
            contract.AddPortForEditor(new BistroBuilderSpatialPortDefinition { portId = ApproachPort,
                kind = BistroBuilderSpatialPortKind.Interaction, radius = 0.32f });
            if (!contract.ValidateDefinition(out string error)) throw new InvalidOperationException(error);
            AssetDatabase.CreateAsset(contract, ContractPath); AssetDatabase.SaveAssets(); return contract;
        }

        public void Apply(GameObject root, SavicManifest manifest)
        {
            if (!PlanMatches(manifest) || root == null || root.GetComponent<RestaurantPlaceableObject>() == null ||
                root.GetComponent<RestaurantPlaceableObject>().HasInstanceId)
                throw new InvalidOperationException("Bar stool authoring requires a current physical plan and an unactivated placeable root.");
            var plan = manifest.barStool;
            Transform visual = root.transform.Find("Visual");
            if (visual == null) throw new InvalidOperationException("Canonical stool Visual is absent.");
            var existing = root.GetComponent<BistroBuilderBarSeatBinding>();
            if (existing != null && existing.AttachedSpot != null) throw new InvalidOperationException("Detach an associated stool before authoring.");
            visual.localPosition = Vector3.zero; visual.localScale = Vector3.one * plan.uniformScale;
            visual.localRotation = Quaternion.Euler(0, plan.visualYawDegrees, 0);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Collider collider in root.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            BoxCollider body = root.AddComponent<BoxCollider>();
            body.size = plan.finalSizeMeters; body.center = Vector3.up * plan.finalSizeMeters.y * 0.5f;
            root.GetComponent<RestaurantPlacementFootprint>().ConfigureRuntime(Vector3.zero,
                new Vector2(plan.finalSizeMeters.x, plan.finalSizeMeters.z), 0.01f);
            var proxy = root.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
            if (proxy == null) proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Simple); proxy.ClearParts();
            proxy.AddPart(new BistroBuilderSpatialProxyPart { partId = "bar.seat.body", localCenter = body.center,
                size = new Vector2(plan.finalSizeMeters.x, plan.finalSizeMeters.z) });
            var physical = root.GetComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
            if (physical == null) physical = root.AddComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
            physical.enabled = true;
            Transform seat = Node(root, "SAVIC_BarSeatFrame", plan.seatLocalPosition);
            Transform approach = Node(root, "SAVIC_BarSeatApproach", plan.approachLocalPosition);
            var binding = existing != null ? existing : root.AddComponent<BistroBuilderBarSeatBinding>();
            binding.ConfigureRuntime(seat, approach, 0.2f, 0.4f, plan.spotPositionToleranceMeters,
                plan.maximumFacingAngleDegrees, plan.approachRadiusMeters);
            binding.RequirePersistedBarRuntime(true);
            var subject = root.GetComponent<BistroBuilderSpatialSubject>();
            subject.ConfigureLifecycleOwner(binding);
            subject.Configure("spatial.bar.seat.template", GetOrCreateContract(), proxy);
            var anchors = root.GetComponent<BistroBuilderSpatialPortAnchors>();
            if (anchors == null) anchors = root.AddComponent<BistroBuilderSpatialPortAnchors>();
            anchors.ClearBindings(); anchors.AddBinding(SeatPort, seat); anchors.AddBinding(ApproachPort, approach);
            if (!Validate(root, manifest, out string error)) throw new InvalidOperationException(error);
        }

        public bool Validate(GameObject root, SavicManifest manifest, out string error)
        {
            error = "Bar stool differs from the physical seat/function plan.";
            if (root == null || !PlanMatches(manifest)) return false;
            var plan = manifest.barStool;
            var visual = root.transform.Find("Visual");
            var binding = root.GetComponent<BistroBuilderBarSeatBinding>();
            var subject = root.GetComponent<BistroBuilderSpatialSubject>();
            var body = root.GetComponent<BoxCollider>();
            if (visual == null || binding == null || !binding.RequiresPersistedBar || subject == null || body == null ||
                root.GetComponent<RestaurantSeat>() != null || root.GetComponent<RestaurantTable>() != null ||
                root.GetComponentsInChildren<BistroBuilderBarServiceSpot>(true).Length != 0 ||
                root.GetComponentsInChildren<Collider>(true).Length != 1 || !body.enabled || body.isTrigger ||
                !Close(body.size, plan.finalSizeMeters) || !Close(body.center, Vector3.up * plan.finalSizeMeters.y * 0.5f) ||
                subject.Contract != AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(ContractPath) ||
                !binding.ValidateConfiguration(out error) || binding.SeatFrame.parent != root.transform || binding.ApproachFrame.parent != root.transform ||
                !Close(binding.SeatFrame.localPosition, plan.seatLocalPosition) || !Close(binding.ApproachFrame.localPosition, plan.approachLocalPosition) ||
                Quaternion.Angle(binding.SeatFrame.localRotation, Quaternion.identity) > 0.001f ||
                Quaternion.Angle(binding.ApproachFrame.localRotation, Quaternion.identity) > 0.001f ||
                !Close(visual.localPosition, Vector3.zero) || !Close(visual.localScale, Vector3.one * plan.uniformScale) ||
                Quaternion.Angle(visual.localRotation, Quaternion.Euler(0, plan.visualYawDegrees, 0)) > 0.001f) return false;
            var proxy = subject.Proxy;
            if (proxy.Parts.Count != 1 || proxy.Parts[0].layer != BistroBuilderSpatialProxyLayer.Static ||
                !Close(proxy.Parts[0].localCenter, body.center) ||
                Vector2.Distance(proxy.Parts[0].size, new Vector2(body.size.x, body.size.z)) > 0.0001f) return false;
            foreach (string port in new [] { SeatPort, ApproachPort })
            {
                Transform frame = port == SeatPort ? binding.SeatFrame : binding.ApproachFrame;
                if (!subject.TryGetPortWorld(port, out var position, out var forward, out _, out _) ||
                    !Close(position, frame.position) || Vector3.Angle(forward, frame.forward) > 0.001f) return false;
            }
            error = string.Empty; return true;
        }

        internal static bool PlanMatches(SavicManifest manifest)
        {
            var plan = manifest?.barStool;
            var profile = AssetDatabase.LoadAssetAtPath<SavicBarStoolProfile>(SavicBarStoolAuthoringPlanner.ProfilePath);
            return plan?.planned == true && profile != null && profile.IsValid && manifest.classification?.type == "BarStool" &&
                plan.plannerVersion == SavicBarStoolAuthoringPlanner.Version && plan.geometryVersion == SavicBarStoolGeometryAnalyzer.Version &&
                plan.profileFingerprint == SavicHashService.ComputeSha256Text(JsonUtility.ToJson(profile)) &&
                plan.inputFingerprint == SavicBarStoolAuthoringPlanner.ComputeFingerprint(plan) &&
                plan.sourceHash == manifest.source?.sourceHash && plan.providerMetadataHash == manifest.source.providerMetadataHash;
        }
        private static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.0001f;
        private static Transform Node(GameObject root, string name, Vector3 position)
        {
            Transform node = root.transform.Find(name);
            if (node == null)
            { var child = new GameObject(name); SceneManager.MoveGameObjectToScene(child, root.scene); child.transform.SetParent(root.transform, false); node = child.transform; }
            node.localPosition = position; node.localRotation = Quaternion.identity; node.localScale = Vector3.one; return node;
        }
    }
}
