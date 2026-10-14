using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    /// <summary>Real source native registration/identity round-trip, not a seated-animation or SaveGame acceptance.</summary>
    internal static class SavicRealBarSeatNativeProbe
    {
        internal static void Verify(GameObject seatTemplate, SavicManifest manifest)
        {
            var context = SavicEditorContext.Instance;
            var barManifest = context.Manifests.GetAll().FirstOrDefault(m => m?.type == "BarCounter" && m.status == "PUBLISHED");
            Require(barManifest != null, "A real published bar is required to check the source stool's native binding.");
            var barPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(barManifest.genericPlaceable.prefabAssetPath);
            Require(barPrefab != null && new SavicBarCounterFunctionAdapter().Validate(barPrefab, barManifest, out _),
                "The real bar prefab lacks its current canonical surface/body authoring.");
            var services = new GameObject("Real stool native source authorities") { hideFlags = HideFlags.HideAndDontSave };
            var item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = "bb_barstool_" + manifest.savicId;
            serialized.FindProperty("prefab").objectReferenceValue = seatTemplate.GetComponent<RestaurantPlaceableObject>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            seatTemplate.GetComponent<RestaurantPlaceableObject>().SetItemDefinition(item);
            var placeables = services.AddComponent<RestaurantPlaceableRegistry>();
            var bars = services.AddComponent<BistroBuilderBarServiceRegistry>();
            var spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
            var assessment = services.AddComponent<BistroBuilderSpatialPlacementAssessmentService>(); assessment.ConfigureForEditor(spatial);
            GameObject barRoot = null, seatRoot = null, groupRoot = null;
            BistroBuilderBarSpatialAdapter adapter = null;
            try
            {
                string barId = "real_source_bar_" + manifest.savicId;
                string seatId = "real_source_seat_" + manifest.savicId;
                var barPosition = new Vector3(1000, 0, 1000);
                var barRotation = Quaternion.Euler(0, 90, 0);
                barRoot = RestoreBar(barPrefab, barId, barPosition, barRotation, placeables, bars, spatial, assessment);
                var barBinding = barRoot.GetComponent<BistroBuilderBarPlaceableBinding>();
                var spot = barBinding.Spots[0];
                var seatRotation = Quaternion.LookRotation(spot.CustomerPoint.forward);
                var seatPosition = spot.CustomerPoint.position - seatRotation *
                    new Vector3(manifest.barStool.seatLocalPosition.x, 0, manifest.barStool.seatLocalPosition.z);
                seatRoot = RestoreSeat(seatTemplate, seatId, seatPosition, seatRotation, placeables, bars, spatial, assessment);
                var seat = seatRoot.GetComponent<BistroBuilderBarSeatBinding>();
                string error = string.Empty;
                Require(seat.AttachedSpot == spot && seat.ValidateRuntimeAssociation(out error) &&
                    bars.FreeCapacity == 1 && bars.RegisteredSpotCount == 1 && spatial.SubjectCount == 3,
                    "Real normalized stool failed automatic association to the real bar: " + error);
                groupRoot = new GameObject("Real source stool native customer") { hideFlags = HideFlags.HideAndDontSave };
                var group = groupRoot.AddComponent<CustomerGroup>();
                Require(group.Initialize(99970, 1, BistroBuilderServiceMode.WaitingAtBar) && bars.TryAllocateSpot(group, out var allocated) &&
                    allocated == spot && seat.Occupant == group, "Real source stool did not use native bar occupancy.");
                adapter = spot.GetComponent<BistroBuilderBarSpatialAdapter>();
                Require(adapter.TryAcquireCustomerLease(group, out error) && !seat.CanDeactivate(out _), error);
                Require(RestaurantBarSeatPersistence.TryCapture(seatRoot.GetComponent<RestaurantPlaceableObject>(), out var link, out error), error);
                var data = new RestaurantStructureSaveData { sceneName = "real_source_native_roundtrip" };
                data.barSeatLinks.Add(link);
                var barItem = barRoot.GetComponent<RestaurantPlaceableObject>().ItemDefinition;
                data.placeables.Add(Record(barId, barItem.ItemId, barPosition, barRotation));
                data.placeables.Add(Record(seatId, item.ItemId, seatPosition, seatRotation));
                var serializer = new BistroBuilderJsonSaveSerializer();
                var snapshot = (RestaurantStructureSaveData)serializer.Deserialize(serializer.Serialize(data, true), typeof(RestaurantStructureSaveData));
                Require(RestaurantBarSeatPersistence.ValidateState(snapshot, id => id == item.ItemId ? item : id == barItem.ItemId ? barItem : null,
                    out error), error);
                Require(bars.ReleaseGroup(group), "Native source customer release failed."); adapter.ReleaseCustomerLease();
                int firstBar = barRoot.GetInstanceID(), firstSeat = seatRoot.GetInstanceID();
                string firstSpotId = spot.BarSpotId;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    Require(placeables.UnregisterPlaceable(seatRoot.GetComponent<RestaurantPlaceableObject>()) &&
                        spot.AttachedSeat == null && placeables.UnregisterPlaceable(barRoot.GetComponent<RestaurantPlaceableObject>()),
                        "Native dependency teardown did not detach the real stool before the bar.");
                    Object.DestroyImmediate(seatRoot); Object.DestroyImmediate(barRoot); seatRoot = barRoot = null;
                    Require(spatial.SubjectCount == 0 && spatial.ActiveLeaseCount == 0 && bars.RegisteredSpotCount == 0,
                        "Source native round-trip retained a previous body, spot or lease.");
                    barRoot = RestoreBar(barPrefab, snapshot.placeables[0].instanceId,
                        snapshot.placeables[0].worldPosition.ToVector3(), snapshot.placeables[0].worldRotation.ToQuaternion(),
                        placeables, bars, spatial, assessment);
                    spot = barRoot.GetComponent<BistroBuilderBarPlaceableBinding>().Spots[0];
                    seatRoot = RestoreSeat(seatTemplate, snapshot.placeables[1].instanceId,
                        snapshot.placeables[1].worldPosition.ToVector3(), snapshot.placeables[1].worldRotation.ToQuaternion(),
                        placeables, bars, spatial, assessment);
                    var map = new Dictionary<string, RestaurantPlaceableObject> {
                        [barId] = barRoot.GetComponent<RestaurantPlaceableObject>(), [seatId] = seatRoot.GetComponent<RestaurantPlaceableObject>() };
                    Require(RestaurantBarSeatPersistence.ValidateRestored(snapshot, map, out error) && spot.BarSpotId == firstSpotId &&
                        firstBar != barRoot.GetInstanceID() && firstSeat != seatRoot.GetInstanceID() && spatial.SubjectCount == 3 && bars.FreeCapacity == 1,
                        "Real source native IDs/association did not survive reconstruction into new Unity instances: " + error);
                    Require(bars.TryAllocateSpot(group, out var restoredSpot) && restoredSpot == spot, "Restored native stool allocation failed.");
                    adapter = spot.GetComponent<BistroBuilderBarSpatialAdapter>();
                    Require(adapter.TryAcquireCustomerLease(group, out error), error);
                    Require(bars.ReleaseGroup(group), "Restored native stool release failed."); adapter.ReleaseCustomerLease();
                }
                Debug.Log("[SAVIC] REAL STOOL NATIVE SOURCE ROUND-TRIP - PASS: " + manifest.savicId +
                    ", original-derived normalized body/frame, real published bar/surface, automatic association, native occupancy/lease, " +
                    "minimal JSON IDs, two reconstructions into new Unity objects and cleanup. No seated animation, Play Mode SaveGame or publication claimed.");
            }
            finally
            {
                adapter?.ReleaseCustomerLease();
                if (groupRoot != null) bars.ReleaseGroup(groupRoot.GetComponent<CustomerGroup>());
                if (seatRoot != null) { placeables.UnregisterPlaceable(seatRoot.GetComponent<RestaurantPlaceableObject>()); Object.DestroyImmediate(seatRoot); }
                if (barRoot != null) { placeables.UnregisterPlaceable(barRoot.GetComponent<RestaurantPlaceableObject>()); Object.DestroyImmediate(barRoot); }
                if (groupRoot != null) Object.DestroyImmediate(groupRoot);
                Object.DestroyImmediate(services); Object.DestroyImmediate(item);
            }
        }

        private static GameObject RestoreBar(GameObject prefab, string id, Vector3 position, Quaternion rotation,
            RestaurantPlaceableRegistry placeables, BistroBuilderBarServiceRegistry bars,
            BistroBuilderSpatialInteractionService spatial, BistroBuilderSpatialPlacementAssessmentService assessment)
        {
            var root = Object.Instantiate(prefab, position, rotation); root.hideFlags = HideFlags.HideAndDontSave;
            var binding = root.GetComponent<BistroBuilderBarPlaceableBinding>();
            var contract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(SavicBarCounterFunctionAdapter.ContractPath);
            binding.ConfigureForEditor(binding.Spots.ToArray(), contract, placeables, bars, spatial, assessment);
            root.GetComponent<BistroBuilderBarBodySpatialAdapter>().ConfigureForEditor(binding, contract, placeables, spatial, assessment);
            root.GetComponent<RestaurantPlaceableObject>().AssignInstanceId(id);
            Require(placeables.RegisterPlaceable(root.GetComponent<RestaurantPlaceableObject>()), placeables.LastRegistrationError);
            return root;
        }
        private static GameObject RestoreSeat(GameObject template, string id, Vector3 position, Quaternion rotation,
            RestaurantPlaceableRegistry placeables, BistroBuilderBarServiceRegistry bars,
            BistroBuilderSpatialInteractionService spatial, BistroBuilderSpatialPlacementAssessmentService assessment)
        {
            var root = Object.Instantiate(template, position, rotation); root.hideFlags = HideFlags.HideAndDontSave;
            root.GetComponent<BistroBuilderBarSeatBinding>().ConfigureDependenciesForEditor(placeables, bars, spatial, assessment);
            root.GetComponent<RestaurantPlaceableObject>().AssignInstanceId(id);
            Require(placeables.RegisterPlaceable(root.GetComponent<RestaurantPlaceableObject>()), placeables.LastRegistrationError);
            return root;
        }
        private static RestaurantPlaceableSaveRecord Record(string instance, string item, Vector3 position, Quaternion rotation) =>
            new RestaurantPlaceableSaveRecord { instanceId = instance, itemId = item, worldPosition = new BistroBuilderSaveVector3(position),
                worldRotation = new BistroBuilderSaveQuaternion(rotation), localScale = new BistroBuilderSaveVector3(Vector3.one) };
        private static void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException(error); }
    }
}
