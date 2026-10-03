using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBarPlaceableBindingSelfTest
    {
        private const string ContractPath = "Assets/Resources/BistroBuilder/Spatial/Contracts/BB_SpatialContract_Bar_Service_Spot.asset";
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Bar Placeable Binding Self-Test", false, 139)]
        public static void RunFromMenu() => RunFromCommandLine();

        public static void VerifyCanonicalRegressionsFromCommandLine()
        {
            SavicV1ClosureGate.RunFromMenu();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            BistroBuilderBarServiceSelfTest.RunFromCommandLine();
            Debug.Log("[SAVIC] BAR BINDING CANONICAL REGRESSIONS - PASS: SAVIC closure gate and installed canonical bar service tests.");
        }

        public static void RunFromCommandLine()
        {
            List<GameObject> objects = new List<GameObject>();
            List<BistroBuilderBarPlaceableBinding> bindings = new List<BistroBuilderBarPlaceableBinding>();
            RestaurantPlaceableItemDefinition item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            try
            {
                BistroBuilderSpatialContractDefinition contract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(ContractPath);
                Require(contract != null && contract.ValidateDefinition(out _), "Canonical bar contract is missing or invalid.");
                GameObject services = MakeObject("Bar binding test services", objects);
                RestaurantPlaceableRegistry placeables = services.AddComponent<RestaurantPlaceableRegistry>();
                BistroBuilderBarServiceRegistry bars = services.AddComponent<BistroBuilderBarServiceRegistry>();
                BistroBuilderSpatialInteractionService spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
                BistroBuilderSpatialPlacementAssessmentService assessment = services.AddComponent<BistroBuilderSpatialPlacementAssessmentService>();
                BistroBuilderBarPlaceableBinding first = MakeBar("binding_a", Vector3.zero, 1, item, objects, bindings);
                first.ConfigureForEditor(ToArray(first.Spots), contract, placeables, bars, spatial, assessment);
                Require(first.CanActivate(out string error), error);
                Require(!first.TryRegisterRuntime(out _) && bars.RegisteredSpotCount == 0 && spatial.SubjectCount == 0,
                    "Provisional bar registered functional destinations.");
                RestaurantPlaceableObject firstPlaceable = first.GetComponent<RestaurantPlaceableObject>();
                Require(placeables.RegisterPlaceable(firstPlaceable), "First placeable could not be registered.");
                Require(first.IsRuntimeRegistered && bars.RegisteredSpotCount == 1 && spatial.SubjectCount == 1,
                    "Placeable registration did not activate its canonical bar spot.");
                Require(first.TryRegisterRuntime(out error) && bars.RegisteredSpotCount == 1 && spatial.SubjectCount == 1, error);
                string firstId = first.Spots[0].BarSpotId;
                Require(firstId == BistroBuilderBarPlaceableBinding.BuildSpotId("binding_a", 0), "Spot identity is not derived from instance identity.");

                BistroBuilderBarPlaceableBinding second = MakeBar("binding_b", new Vector3(8, 0, 0), 1, item, objects, bindings);
                second.ConfigureForEditor(ToArray(second.Spots), contract, placeables, bars, spatial, assessment);
                Require(placeables.RegisterPlaceable(second.GetComponent<RestaurantPlaceableObject>()), "Second placeable could not be registered.");
                Require(second.IsRuntimeRegistered && first.Spots[0].BarSpotId != second.Spots[0].BarSpotId && bars.RegisteredSpotCount == 2,
                    "Two copies did not receive distinct service identities.");

                CustomerGroup group = MakeObject("Bar binding test group", objects).AddComponent<CustomerGroup>();
                Require(group.Initialize(99001, 1, BistroBuilderServiceMode.WaitingAtBar), "Test group initialization failed.");
                Require(bars.TryAllocateSpot(group, BistroBuilderServiceMode.WaitingAtBar, out BistroBuilderBarServiceSpot anchor),
                    "Canonical registry could not allocate a newly placed bar spot.");
                BistroBuilderBarPlaceableBinding occupied = anchor.GetComponentInParent<BistroBuilderBarPlaceableBinding>();
                BistroBuilderBarSpatialAdapter adapter = anchor.GetComponent<BistroBuilderBarSpatialAdapter>();
                Require(adapter.TryAcquireCustomerLease(group, out error), error);
                Require(spatial.ActiveLeaseCount == 1 && !occupied.CanDeactivate(out _), "Occupied bar was allowed to deactivate.");
                Require(!anchor.TryConfigure("changed_occupied_identity", anchor.CustomerPoint, anchor.WaiterServicePoint, 1, true),
                    "An occupied spot allowed its identity to be replaced.");
                Require(bars.ReleaseGroup(group), "Canonical group release failed.");
                Require(!occupied.CanDeactivate(out _), "An active spatial lease was ignored after logical group release.");
                adapter.ReleaseCustomerLease();
                Require(occupied.CanDeactivate(out error), error);
                Require(placeables.UnregisterPlaceable(firstPlaceable), "Placeable unregistration failed.");
                Require(!first.IsRuntimeRegistered && bars.RegisteredSpotCount == 1 && spatial.SubjectCount == 1,
                    "Deactivated bar left service or spatial registrations behind.");
                Require(placeables.RegisterPlaceable(firstPlaceable) && first.TryRegisterRuntime(out error), error);
                Require(first.Spots[0].BarSpotId == firstId, "Reactivation changed the persistent service identity.");
                first.ReleaseRuntimeRegistration(); second.ReleaseRuntimeRegistration();
                Require(bars.RegisteredSpotCount == 0 && spatial.SubjectCount == 0 && spatial.ActiveLeaseCount == 0,
                    "Release left canonical records or leases behind.");

                // Seed only the second spatial identity. Registration must roll back the first spot too.
                BistroBuilderBarPlaceableBinding multiple = MakeBar("binding_c", new Vector3(16, 0, 0), 2, item, objects, bindings);
                RestaurantPlaceableObject multiPlaceable = multiple.GetComponent<RestaurantPlaceableObject>();
                multiple.ConfigureForEditor(ToArray(multiple.Spots), contract, placeables, bars, spatial, assessment);
                Require(placeables.RegisterPlaceable(multiPlaceable), "Multi-spot placeable could not be registered.");
                multiple.ReleaseRuntimeRegistration();
                GameObject blocker = MakeObject("Spatial identity blocker", objects);
                BistroBuilderAdaptiveSpatialProxy proxy = blocker.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
                BistroBuilderSpatialSubject subject = blocker.AddComponent<BistroBuilderSpatialSubject>();
                subject.Configure("spatial." + BistroBuilderBarPlaceableBinding.BuildSpotId("binding_c", 1), contract, proxy);
                Require(spatial.RegisterSubject(subject), "Spatial identity blocker could not be registered.");
                Require(!multiple.TryRegisterRuntime(out _) && bars.RegisteredSpotCount == 0 && !multiple.IsRuntimeRegistered && spatial.SubjectCount == 1,
                    "Identity-conflicting bar activation produced partial registrations.");
                spatial.UnregisterSubject(subject);
                subject.Configure(string.Empty, contract, proxy);
                Require(multiple.TryRegisterRuntime(out error) && bars.RegisteredSpotCount == 2, error);
                multiple.ReleaseRuntimeRegistration();
                Require(bars.RegisteredSpotCount == 0 && spatial.SubjectCount == 0 && assessment.CachedProviderCount == 0,
                    "Multi-spot cleanup left a registry entry or semantic provider behind.");
                Debug.Log("[SAVIC] BAR PLACEABLE BINDING SELF-TEST - PASS: provisional isolation, activation events, two copies, idempotence, " +
                    "canonical group allocation and BBSIS lease, occupied guards, stable reactivation, partial rollback and cleanup. No bar asset published.");
            }
            finally
            {
                foreach (BistroBuilderBarPlaceableBinding binding in bindings) if (binding != null) binding.ReleaseRuntimeRegistration();
                for (int index = objects.Count - 1; index >= 0; index--) if (objects[index] != null) Object.DestroyImmediate(objects[index]);
                Object.DestroyImmediate(item);
            }
        }

        private static BistroBuilderBarPlaceableBinding MakeBar(string instance, Vector3 position, int count,
            RestaurantPlaceableItemDefinition item, List<GameObject> objects, List<BistroBuilderBarPlaceableBinding> bindings)
        {
            GameObject root = MakeObject("Synthetic bar " + instance, objects); root.transform.position = position;
            RestaurantPlaceableObject placeable = root.AddComponent<RestaurantPlaceableObject>();
            placeable.SetItemDefinition(item); placeable.AssignInstanceId(instance);
            BistroBuilderBarServiceSpot[] spots = new BistroBuilderBarServiceSpot[count];
            for (int index = 0; index < count; index++)
            {
                GameObject spotRoot = new GameObject("Spot " + index); spotRoot.transform.SetParent(root.transform, false);
                spotRoot.transform.localPosition = Vector3.right * index * 1.2f;
                Transform customer = new GameObject("Customer").transform; customer.SetParent(spotRoot.transform, false);
                customer.localPosition = new Vector3(0, 0, 0.75f);
                Transform waiter = new GameObject("Waiter").transform; waiter.SetParent(spotRoot.transform, false);
                waiter.localPosition = new Vector3(0, 0, -0.75f);
                spots[index] = spotRoot.AddComponent<BistroBuilderBarServiceSpot>();
                Require(spots[index].TryConfigure("bar.template.slot_" + index, customer, waiter, 1, true), "Synthetic spot is invalid.");
            }
            BistroBuilderBarPlaceableBinding binding = root.AddComponent<BistroBuilderBarPlaceableBinding>();
            // Authoring configuration is supplied after the caller establishes registration ordering.
            SerializedObject authored = new SerializedObject(binding);
            SerializedProperty property = authored.FindProperty("spots"); property.arraySize = count;
            for (int index = 0; index < count; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = spots[index];
            authored.ApplyModifiedPropertiesWithoutUndo();
            bindings.Add(binding); return binding;
        }
        private static BistroBuilderBarServiceSpot[] ToArray(IReadOnlyList<BistroBuilderBarServiceSpot> spots)
        { BistroBuilderBarServiceSpot[] result = new BistroBuilderBarServiceSpot[spots.Count]; for (int index = 0; index < result.Length; index++) result[index] = spots[index]; return result; }
        private static GameObject MakeObject(string name, List<GameObject> objects)
        { GameObject result = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(result); return result; }
        private static void Require(bool success, string error) { if (!success) throw new InvalidOperationException(error); }
    }
}
