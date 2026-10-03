using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBarSeatPersistenceSelfTest
    {
        public static void RunFromCommandLine()
        {
            var roots = new List<GameObject>();
            var assets = new List<Object>();
            try
            {
                var barRoot = Make("Persisted native bar prototype", roots);
                var barPlaceable = barRoot.AddComponent<RestaurantPlaceableObject>();
                var barItem = MakeItem("bar_persistence_test", barPlaceable, assets);
                barPlaceable.SetItemDefinition(barItem);
                var slot = Node(barRoot.transform, "Slot", Vector3.zero);
                var customer = Node(slot, "Customer", new Vector3(0, 0, 0.75f));
                var waiter = Node(slot, "Waiter", new Vector3(0, 0, -0.75f));
                var surface = Node(slot, "CounterSurface", new Vector3(0, 1.05f, 0));
                var spot = slot.gameObject.AddComponent<BistroBuilderBarServiceSpot>();
                Require(spot.TryConfigure("bar.template.persisted", customer, waiter, 1, true) &&
                    spot.TryConfigureCounterSurface(surface), "Native bar persistence prototype is invalid.");
                var binding = barRoot.AddComponent<BistroBuilderBarPlaceableBinding>();
                binding.ConfigureForEditor(new[] { spot }, AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(
                    SavicBarCounterFunctionAdapter.ContractPath));
                var seatRoot = Make("Persisted native stool prototype", roots);
                var seatPlaceable = seatRoot.AddComponent<RestaurantPlaceableObject>();
                var seatItem = MakeItem("seat_persistence_test", seatPlaceable, assets);
                seatPlaceable.SetItemDefinition(seatItem);
                var proxy = seatRoot.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
                proxy.AddPart(new BistroBuilderSpatialProxyPart { partId = "body", size = Vector2.one * 0.4f });
                seatRoot.GetComponent<RestaurantPlacementFootprint>().ConfigureRuntime(Vector3.zero, Vector2.one * 0.4f, 0);
                seatRoot.AddComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
                var subject = seatRoot.AddComponent<BistroBuilderSpatialSubject>();
                var seat = seatRoot.AddComponent<BistroBuilderBarSeatBinding>();
                var seatContract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>(); assets.Add(seatContract);
                seatContract.ConfigureForEditor("bar_seat_persistence_test", BistroBuilderBarSeatBinding.SpatialFamilyId,
                    BistroBuilderAdaptiveSpatialProxyMode.Simple, new[] { "seating.bar" });
                seat.ConfigureRuntime(Node(seatRoot.transform, "Seat", new Vector3(0, 0.75f, 0)),
                    Node(seatRoot.transform, "Approach", new Vector3(0, 0, -0.65f)), 0.2f, 0.4f, 0.08f, 10, 0.32f);
                subject.Configure("spatial.bar.seat.template", seatContract, proxy);
                var barPosition = new Vector3(30, 0, 30);
                var rotation = Quaternion.Euler(0, 90, 0);
                var data = new RestaurantStructureSaveData { sceneName = "native_seat_persistence_test" };
                data.placeables.Add(Record("a_stool", seatItem.ItemId, barPosition + rotation * customer.localPosition, rotation));
                data.placeables.Add(Record("z_bar", barItem.ItemId, barPosition, rotation));
                data.barSeatLinks.Add(new RestaurantBarSeatLinkSaveRecord { seatInstanceId = "a_stool", barInstanceId = "z_bar", spotIndex = 0 });
                Func<string, RestaurantPlaceableItemDefinition> resolve = id => id == barItem.ItemId ? barItem : id == seatItem.ItemId ? seatItem : null;
                var serializer = new BistroBuilderJsonSaveSerializer();
                var reloaded = (RestaurantStructureSaveData)serializer.Deserialize(serializer.Serialize(data, true), typeof(RestaurantStructureSaveData));
                Require(RestaurantBarSeatPersistence.ValidateState(reloaded, resolve, out string error) &&
                    reloaded.barSeatLinks[0].seatInstanceId == "a_stool" && reloaded.barSeatLinks[0].barInstanceId == "z_bar",
                    "Rotated native bar/seat relationship did not survive JSON round-trip: " + error);
                var originalSeatPose = seatRoot.transform.position;
                var originalBarPose = barRoot.transform.position;
                reloaded.barSeatLinks.Add(reloaded.barSeatLinks[0]);
                Require(!RestaurantBarSeatPersistence.ValidateState(reloaded, resolve, out _), "Duplicate native seat/spot links were accepted.");
                reloaded.barSeatLinks.RemoveAt(1);
                reloaded.barSeatLinks[0].spotIndex = 1;
                Require(!RestaurantBarSeatPersistence.ValidateState(reloaded, resolve, out _), "Unknown native spot was accepted.");
                reloaded.barSeatLinks[0].spotIndex = 0;
                reloaded.placeables[0].worldPosition.y += 0.3f;
                Require(!RestaurantBarSeatPersistence.ValidateState(reloaded, resolve, out _), "Invalid saved seat/counter height was accepted.");
                reloaded.placeables[0].worldPosition.y -= 0.3f;
                reloaded.placeables[0].worldRotation = new BistroBuilderSaveQuaternion(Quaternion.Euler(0, 270, 0));
                Require(!RestaurantBarSeatPersistence.ValidateState(reloaded, resolve, out _), "Wrong-facing persisted stool was accepted.");
                reloaded.placeables[0].worldRotation = new BistroBuilderSaveQuaternion(rotation);
                reloaded.placeables[0].localScale = new BistroBuilderSaveVector3(-Vector3.one);
                Require(!RestaurantBarSeatPersistence.ValidateState(reloaded, resolve, out _), "Negative saved stool scale was accepted.");
                reloaded.placeables[0].localScale = new BistroBuilderSaveVector3(Vector3.one);
                reloaded.barSeatLinks.Clear();
                Require(!RestaurantBarSeatPersistence.ValidateState(reloaded, resolve, out _), "An operational stool without its native relation was accepted.");
                Require(seatRoot.transform.position == originalSeatPose && barRoot.transform.position == originalBarPose &&
                    spot.AttachedSeat == null, "Save prevalidation changed a live/prototype pose or native association.");
                var provider = Make("Structure migration provider", roots).AddComponent<RestaurantStructureSaveSectionProvider>();
                byte[] migrated = null;
                Require(provider.FromVersion == 1 && provider.ToVersion == 2 &&
                    provider.TryMigrate(Encoding.UTF8.GetBytes("{\"sceneName\":\"legacy\",\"placeables\":[],\"seatLinks\":[]}"),
                        out migrated, out error), "Legacy structure v1 migration failed: " + error);
                var legacy = (RestaurantStructureSaveData)serializer.Deserialize(migrated, typeof(RestaurantStructureSaveData));
                Require(legacy.sceneName == "legacy" && legacy.barSeatLinks != null && legacy.barSeatLinks.Count == 0 &&
                    RestaurantBarSeatPersistence.ValidateState(legacy, resolve, out error), "Legacy save gained fabricated bar/seat relationships: " + error);
                Debug.Log("[SAVIC] BAR SEAT PERSISTENCE CONTRACT - PASS: minimal native IDs, rotated pose round-trip, duplicate/missing/unknown/height/facing/scale negatives, " +
                    "non-mutating prevalidation and structure v1-to-v2 migration. Actual stool SaveGame/runtime animation not yet asserted.");
            }
            finally
            {
                for (int index = roots.Count - 1; index >= 0; index--) if (roots[index] != null) Object.DestroyImmediate(roots[index]);
                foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
        private static RestaurantPlaceableSaveRecord Record(string instance, string item, Vector3 position, Quaternion rotation) =>
            new RestaurantPlaceableSaveRecord { instanceId = instance, itemId = item,
                worldPosition = new BistroBuilderSaveVector3(position), worldRotation = new BistroBuilderSaveQuaternion(rotation),
                localScale = new BistroBuilderSaveVector3(Vector3.one) };
        private static RestaurantPlaceableItemDefinition MakeItem(string id, RestaurantPlaceableObject prefab, List<Object> assets)
        {
            var item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>(); assets.Add(item);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo(); return item;
        }
        private static GameObject Make(string name, List<GameObject> roots)
        { var root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; roots.Add(root); return root; }
        private static Transform Node(Transform root, string name, Vector3 position)
        { var node = new GameObject(name).transform; node.SetParent(root, false); node.localPosition = position; return node; }
        private static void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException(error); }
    }
}
