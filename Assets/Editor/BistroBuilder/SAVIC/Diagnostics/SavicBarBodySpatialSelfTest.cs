using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBarBodySpatialSelfTest
    {
        private const string ContractPath = "Assets/Resources/BistroBuilder/Spatial/Contracts/BB_SpatialContract_Bar_Service_Spot.asset";
        public static void RunFromCommandLine() => Run(true);

        public static void VerifyNativeAndCanonicalFromCommandLine()
        {
            Require(Application.isBatchMode, "Isolated native bar-body probe is batch-only.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Run(false);
            SavicCompoundPhysicalFootprintSelfTest.VerifyNativeRegressionsFromCommandLine();
            BistroBuilderBarServiceSelfTest.RunFromCommandLine();
            Debug.Log("[SAVIC] BAR BODY SPATIAL CANONICAL REGRESSIONS - PASS: native rebuild, placement/route, navigation, edit mode and installed bar service.");
        }

        private static void Run(bool hidden)
        {
            List<GameObject> objects = new List<GameObject>();
            List<BistroBuilderBarPlaceableBinding> bindings = new List<BistroBuilderBarPlaceableBinding>();
            RestaurantPlaceableItemDefinition item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            RestaurantAreaDefinition areaDefinition = ScriptableObject.CreateInstance<RestaurantAreaDefinition>();
            try
            {
                BistroBuilderSpatialContractDefinition contract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(ContractPath);
                Require(contract != null && contract.ValidateDefinition(out _), "Canonical bar contract is invalid.");
                GameObject services = Make("Bar body native services", hidden, objects);
                RestaurantPlaceableRegistry placeables = services.AddComponent<RestaurantPlaceableRegistry>();
                BistroBuilderBarServiceRegistry bars = services.AddComponent<BistroBuilderBarServiceRegistry>();
                BistroBuilderSpatialInteractionService spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
                BistroBuilderSpatialPlacementAssessmentService assessment = services.AddComponent<BistroBuilderSpatialPlacementAssessmentService>();
                assessment.ConfigureForEditor(spatial);
                BistroBuilderBarBodySpatialAdapter first = MakeBar("body_a", Vector3.zero, 1, item, contract,
                    placeables, bars, spatial, assessment, hidden, objects, bindings);
                BistroBuilderSpatialSubject firstSubject = first.GetComponent<BistroBuilderSpatialSubject>();
                Require(first.ValidateConfiguration(out string error), error);
                Require(firstSubject.SubjectId == BistroBuilderBarBodySpatialAdapter.BuildBodyId("body_a") &&
                    !firstSubject.IsRegistrationEligible && !spatial.RegisterSubject(firstSubject) &&
                    !first.TryRegisterRoot(out _) && spatial.SubjectCount == 0 && bars.RegisteredSpotCount == 0,
                    "Provisional bar registered a body or functional destination.");
                if (!hidden) { spatial.RebuildSubjects(); Require(spatial.SubjectCount == 0, "Scene rebuild discovered provisional bar geometry."); }
                List<BistroBuilderSpatialSemanticVolume> semantics = new List<BistroBuilderSpatialSemanticVolume>();
                Require(first.WriteSemanticVolumes(semantics) == 3, "Provisional candidate lost the ports needed for preflight.");
                Transform badPoint = first.GetComponent<BistroBuilderBarPlaceableBinding>().Spots[0].WaiterServicePoint;
                badPoint.localPosition = new Vector3(0f, 0f, 1.75f);
                Require(!first.CanActivate(out _), "A waiter point inside the physical counter was accepted.");
                badPoint.localPosition = Vector3.zero;
                Require(placeables.RegisterPlaceable(first.GetComponent<RestaurantPlaceableObject>()) &&
                    spatial.SubjectCount == 2 && bars.RegisteredSpotCount == 1, "Confirmed bar body/spot were not registered together.");
                Require(first.TryRegisterRoot(out error) && first.GetComponent<BistroBuilderBarPlaceableBinding>().TryRegisterRuntime(out error) &&
                    spatial.SubjectCount == 2 && bars.RegisteredSpotCount == 1, "Confirmed registration is not idempotent: " + error);
                BistroBuilderBarServiceSpot spot = first.GetComponent<BistroBuilderBarPlaceableBinding>().Spots[0];
                BistroBuilderAdaptiveSpatialProxy childProxy = spot.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
                List<BistroBuilderSpatialVolume> childBody = new List<BistroBuilderSpatialVolume>();
                Require(childProxy.ValidateProxy(out _) && childProxy.BuildWorldVolumes(BistroBuilderSpatialProxyLayer.Static, childBody) == 0 &&
                    spot.GetComponent<BistroBuilderBarSpatialAdapter>().WriteSemanticVolumes(new List<BistroBuilderSpatialSemanticVolume>()) == 0,
                    "Child plaza duplicated physical body or scene semantics.");
                Require(spatial.TryFindStaticGeometryConflict(BistroBuilderSpatialVolume.Circle(new Vector3(1.75f, 0f, 0f), 0.1f),
                    string.Empty, string.Empty, out _) &&
                    !spatial.TryFindStaticGeometryConflict(BistroBuilderSpatialVolume.Circle(Vector3.zero, 0.28f), string.Empty, string.Empty, out _),
                    "Registered BBSIS body lost a wall or filled the interior.");

                GameObject areaObject = Make("Bar body preflight area", hidden, objects);
                BoxCollider boundary = areaObject.AddComponent<BoxCollider>(); boundary.size = new Vector3(24f, 2f, 24f); boundary.isTrigger = true;
                RestaurantArea area = areaObject.AddComponent<RestaurantArea>();
                SerializedObject areaData = new SerializedObject(area);
                areaData.FindProperty("areaId").stringValue = "bar_body_probe";
                areaData.FindProperty("definition").objectReferenceValue = areaDefinition;
                areaData.FindProperty("boundaryColliders").arraySize = 1;
                areaData.FindProperty("boundaryColliders").GetArrayElementAtIndex(0).objectReferenceValue = boundary;
                areaData.ApplyModifiedPropertiesWithoutUndo(); Physics.SyncTransforms();
                Require(assessment.EvaluateCandidate(firstSubject, Vector3.zero, Quaternion.identity, area, null).IsValid,
                    "Confirmed bar's own ports conflicted with its body or duplicated semantics.");
                BistroBuilderBarBodySpatialAdapter provisional = MakeBar("body_b", Vector3.right * 6f, 1, item, contract,
                    placeables, bars, spatial, assessment, hidden, objects, bindings);
                assessment.RegisterProvider(provisional);
                GameObject blockerObject = Make("Native preflight blocker", hidden, objects); blockerObject.transform.position = Vector3.right * 6f;
                BistroBuilderAdaptiveSpatialProxy blockerProxy = blockerObject.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
                blockerProxy.AddPart(Part("blocker", Vector3.zero, Vector2.one * 0.4f));
                BistroBuilderSpatialSubject blocker = blockerObject.AddComponent<BistroBuilderSpatialSubject>();
                blocker.Configure("bar_body_native_blocker", contract, blockerProxy); Require(spatial.RegisterSubject(blocker), "Blocker could not be registered.");
                Require(assessment.EvaluateCandidate(blocker, blockerObject.transform.position, Quaternion.identity, area, null).IsValid,
                    "Provisional bar leaked scene semantics into another proposal.");
                Require(!assessment.EvaluateCandidate(provisional.GetComponent<BistroBuilderSpatialSubject>(), provisional.transform.position,
                    Quaternion.identity, area, null).IsValid, "Provisional candidate bypassed its own critical waiter-space preflight.");
                spatial.UnregisterSubject(blocker); blocker.Configure(string.Empty, contract, blockerProxy);
                if (!hidden)
                {
                    spatial.RebuildSubjects(); Require(spatial.SubjectCount == 2, "Scene rebuild lost a confirmed body or discovered a provisional one.");
                    BistroBuilderNavigationService navigation = services.AddComponent<BistroBuilderNavigationService>();
                    navigation.RebuildNavigationTopology(); Require(navigation.StaticObstacleCount == 3,
                        "Navigation included the provisional bar in scene topology.");
                    BistroBuilderSpatialAssessmentService quality = services.AddComponent<BistroBuilderSpatialAssessmentService>();
                    Require(quality.EvaluateCurrentLayout().viable, "Scene quality found duplicate or provisional bar semantics.");
                }

                GameObject groupObject = Make("Native bar body customer", hidden, objects);
                CustomerGroup group = groupObject.AddComponent<CustomerGroup>();
                Require(group.Initialize(99011, 1, BistroBuilderServiceMode.WaitingAtBar), "Native group initialization failed.");
                Require(bars.TryAllocateSpot(group, BistroBuilderServiceMode.WaitingAtBar, out BistroBuilderBarServiceSpot allocated) && allocated == spot,
                    "Canonical bar allocation failed.");
                Require(spot.GetComponent<BistroBuilderBarSpatialAdapter>().TryAcquireCustomerLease(group, out error),
                    "Canonical customer lease failed against the measured body: " + error);
                Require(!first.CanDeactivate(out _), "Busy compound bar allowed removal.");
                bars.ReleaseGroup(group);
                spot.GetComponent<BistroBuilderBarSpatialAdapter>().ReleaseCustomerLease();
                Require(first.CanDeactivate(out error), error);
                placeables.UnregisterPlaceable(first.GetComponent<RestaurantPlaceableObject>());
                Require(spatial.SubjectCount == 0 && bars.RegisteredSpotCount == 0 && string.IsNullOrEmpty(firstSubject.SubjectId),
                    "Removal left body, plaza, semantic provider or spatial identity behind.");

                Require(placeables.RegisterPlaceable(provisional.GetComponent<RestaurantPlaceableObject>()) &&
                    spatial.SubjectCount == 2 && provisional.SpatialSubjectId != first.SpatialSubjectId,
                    "Second copy could not acquire its own stable identities.");
                placeables.UnregisterPlaceable(provisional.GetComponent<RestaurantPlaceableObject>());
                TestPartialRollback(item, contract, placeables, bars, spatial, assessment, hidden, objects, bindings);
                Require(spatial.SubjectCount == 0 && bars.RegisteredSpotCount == 0, "Compound lifecycle test leaked native registrations.");
                Debug.Log("[SAVIC] BAR BODY SPATIAL SELF-TEST - PASS: provisional/preflight, root body and native ports, no child duplicates, " +
                    "canonical allocation/lease, busy guard, identities, rollback and cleanup" + (hidden ? "." : ", real scene rebuild/navigation/quality."));
            }
            finally
            {
                foreach (BistroBuilderBarPlaceableBinding binding in bindings) if (binding != null) binding.ReleaseRuntimeRegistration();
                for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
                Object.DestroyImmediate(item); Object.DestroyImmediate(areaDefinition);
            }
        }

        private static void TestPartialRollback(RestaurantPlaceableItemDefinition item, BistroBuilderSpatialContractDefinition contract,
            RestaurantPlaceableRegistry placeables, BistroBuilderBarServiceRegistry bars, BistroBuilderSpatialInteractionService spatial,
            BistroBuilderSpatialPlacementAssessmentService assessment, bool hidden, List<GameObject> objects, List<BistroBuilderBarPlaceableBinding> bindings)
        {
            BistroBuilderBarBodySpatialAdapter multiple = MakeBar("body_partial", Vector3.zero, 2, item, contract,
                placeables, bars, spatial, assessment, hidden, objects, bindings);
            BistroBuilderBarPlaceableBinding binding = multiple.GetComponent<BistroBuilderBarPlaceableBinding>();
            BistroBuilderSpatialSubject blocker = null;
            Action<BistroBuilderBarServiceSpot> intercept = registered =>
            {
                if (registered != binding.Spots[0] || blocker != null) return;
                GameObject node = Make("Mid-transaction identity blocker", hidden, objects);
                BistroBuilderAdaptiveSpatialProxy proxy = node.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
                proxy.AddPart(Part("blocker", Vector3.zero, Vector2.one));
                blocker = node.AddComponent<BistroBuilderSpatialSubject>();
                blocker.Configure("spatial." + BistroBuilderBarPlaceableBinding.BuildSpotId("body_partial", 1), contract, proxy);
                Require(spatial.RegisterSubject(blocker), "Mid-transaction blocker failed.");
            };
            // Register the placeable without firing its binding first; exercise an explicit transaction.
            binding.enabled = false;
            Require(placeables.RegisterPlaceable(multiple.GetComponent<RestaurantPlaceableObject>()), "Partial test placeable failed.");
            binding.enabled = true;
            bars.SpotRegistered += intercept;
            try
            {
                Require(!binding.TryRegisterRuntime(out _) && blocker != null && spatial.SubjectCount == 1 &&
                    bars.RegisteredSpotCount == 0 && !binding.IsRuntimeRegistered && string.IsNullOrEmpty(multiple.SpatialSubjectId),
                    "A mid-transaction plaza conflict retained the new root or an earlier plaza.");
            }
            finally { bars.SpotRegistered -= intercept; }
            if (blocker != null) { spatial.UnregisterSubject(blocker); blocker.Configure(string.Empty, contract, blocker.Proxy); }
            Require(binding.TryRegisterRuntime(out string error) && spatial.SubjectCount == 3 && bars.RegisteredSpotCount == 2,
                "Rolled-back compound bar could not retry cleanly: " + error);
            placeables.UnregisterPlaceable(multiple.GetComponent<RestaurantPlaceableObject>());
        }

        private static BistroBuilderBarBodySpatialAdapter MakeBar(string id, Vector3 position, int count,
            RestaurantPlaceableItemDefinition item, BistroBuilderSpatialContractDefinition contract, RestaurantPlaceableRegistry placeables,
            BistroBuilderBarServiceRegistry bars, BistroBuilderSpatialInteractionService spatial, BistroBuilderSpatialPlacementAssessmentService assessment,
            bool hidden, List<GameObject> objects, List<BistroBuilderBarPlaceableBinding> bindings)
        {
            GameObject root = Make("Compound bar " + id, hidden, objects); root.transform.position = position;
            RestaurantPlaceableObject placeable = root.AddComponent<RestaurantPlaceableObject>(); placeable.SetItemDefinition(item);
            root.GetComponent<RestaurantPlacementFootprint>().ConfigureRuntime(Vector3.zero, new Vector2(4f, 4f));
            BistroBuilderAdaptiveSpatialProxy proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Compound);
            proxy.AddPart(Part("left", new Vector3(-1.75f, 0f, 0f), new Vector2(0.5f, 4f)));
            proxy.AddPart(Part("right", new Vector3(1.75f, 0f, 0f), new Vector2(0.5f, 4f)));
            proxy.AddPart(Part("front", new Vector3(0f, 0f, 1.75f), new Vector2(3f, 0.5f)));
            BistroBuilderBarServiceSpot[] spots = new BistroBuilderBarServiceSpot[count];
            for (int i = 0; i < count; i++)
            {
                GameObject spotRoot = new GameObject("Native bar spot " + i); spotRoot.transform.SetParent(root.transform, false);
                float x = count == 1 ? 0f : (i - 0.5f) * 1.2f;
                Transform customer = new GameObject("Customer").transform; customer.SetParent(root.transform, false); customer.localPosition = new Vector3(x, 0f, 2.8f);
                Transform waiter = new GameObject("Waiter").transform; waiter.SetParent(root.transform, false); waiter.localPosition = new Vector3(x, 0f, 0f);
                spots[i] = spotRoot.AddComponent<BistroBuilderBarServiceSpot>();
                Require(spots[i].TryConfigure("bar.template." + i, customer, waiter, 1, true), "Bar spot authoring failed.");
            }
            BistroBuilderBarPlaceableBinding binding = root.AddComponent<BistroBuilderBarPlaceableBinding>();
            binding.ConfigureForEditor(spots, contract, placeables, bars, spatial, assessment); bindings.Add(binding);
            BistroBuilderBarBodySpatialAdapter body = root.AddComponent<BistroBuilderBarBodySpatialAdapter>();
            body.ConfigureForEditor(binding, contract, placeables, spatial, assessment);
            placeable.AssignInstanceId(id);
            return body;
        }
        private static BistroBuilderSpatialProxyPart Part(string id, Vector3 center, Vector2 size) =>
            new BistroBuilderSpatialProxyPart { partId = id, localCenter = center, size = size };
        private static GameObject Make(string name, bool hidden, List<GameObject> objects)
        { GameObject result = new GameObject(name); if (hidden) result.hideFlags = HideFlags.HideAndDontSave; objects.Add(result); return result; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
