using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBarSeatBindingSelfTest
    {
        public static void VerifyNativeAndCanonicalFromCommandLine()
        {
            Require(Application.isBatchMode, "The isolated native regression probe is batch-only.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RunFromCommandLine();
            SavicV1ClosureGate.RunFromMenu();
            SavicCompoundPhysicalFootprintSelfTest.VerifyNativeRegressionsFromCommandLine();
            BistroBuilderBarServiceSelfTest.RunFromCommandLine();
            BistroBuilderBBSISPhase2BSelfTest.Run();
            Require(BistroBuilderBBSISPhase2BSelfTest.LastFailed == 0, BistroBuilderBBSISPhase2BSelfTest.LastReport);
            var context = SavicEditorContext.Instance;
            foreach (var manifest in context.Manifests.GetAll())
                if (manifest?.status == "PUBLISHED" && SavicFunctionalRuntimeAcceptance.Required(manifest))
                    Require(SavicFunctionalRuntimeAcceptance.Matches(manifest, context.Layout),
                        "Published functional source/plan/profile/prefab/report runtime acceptance became stale.");
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
            Debug.Log("[SAVIC] BAR SEAT NATIVE AND CANONICAL REGRESSIONS - PASS: native contracts and current proofs of published functional assets.");
        }

        public static void RunDynamicCoordinatorFromCommandLine()
        {
            List<GameObject> objects = new List<GameObject>();
            BistroBuilderBarSpatialAdapter adapter = null;
            BistroBuilderBarServiceRegistry bars = null;
            BistroBuilderSpatialInteractionService spatial = null;
            BistroBuilderOperationalSpatialCoordinator coordinator = null;
            CustomerGroup group = null;
            try
            {
                GameObject services = Make("Dynamic bar coordinator services", objects);
                bars = services.AddComponent<BistroBuilderBarServiceRegistry>();
                spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
                var barSystem = services.AddComponent<BistroBuilderBarServiceSystem>();
                coordinator = services.AddComponent<BistroBuilderOperationalSpatialCoordinator>();
                coordinator.ConfigureForEditor(spatial, null, null, bars, barSystem);
                var contract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(
                    "Assets/Resources/BistroBuilder/Spatial/Contracts/BB_SpatialContract_Bar_Service_Spot.asset");
                GameObject root = Make("Bar confirmed after operational bootstrap", objects);
                // ResetTransientRuntimeStateAfterLoad rebuilds native scene subjects.
                // The fixture must participate in that real discovery, like a runtime bar.
                root.hideFlags = HideFlags.None;
                root.transform.position = new Vector3(15, 0, 15);
                var spot = root.AddComponent<BistroBuilderBarServiceSpot>();
                Require(spot.TryConfigure("bar.dynamic.coordinator.test", Node(root, "Customer", Vector3.forward),
                    Node(root, "Waiter", Vector3.back), 1, true), "Invalid late bar fixture.");
                var proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
                proxy.AddPart(new BistroBuilderSpatialProxyPart { partId = "operational", layer = BistroBuilderSpatialProxyLayer.Operational,
                    size = new Vector2(0.2f, 0.2f) });
                var subject = root.AddComponent<BistroBuilderSpatialSubject>();
                subject.Configure("spatial.bar.dynamic.coordinator.test", contract, proxy);
                adapter = root.AddComponent<BistroBuilderBarSpatialAdapter>(); adapter.Configure(spot, subject, spatial);
                Require(spatial.RegisterSubject(subject) && bars.TryRegisterSpot(spot, out _), "Late bar registration failed.");
                group = MakeGroup(99930, objects); group.transform.position = spot.CustomerPoint.position;
                Require(bars.TryAllocateSpot(group, out var allocated) && allocated == spot, "Native late bar allocation failed.");
                coordinator.ReconcileOperationalClaims();
                Require(adapter.HasCustomerLease && spatial.CountLeases(BistroBuilderSpatialClaimKind.Seat) == 1,
                    "Operational coordinator did not grant the newly registered bar's customer lease without a manual rebuild.");
                Require(bars.ReleaseGroup(group), "Native late bar release failed.");
                coordinator.ReconcileOperationalClaims();
                Require(!adapter.HasCustomerLease && spatial.ActiveLeaseCount == 0,
                    "Operational coordinator retained a permanent customer lease after the native bar spot was released.");
                Require(bars.TryAllocateSpot(group, out _) , "Repeated native allocation failed.");
                coordinator.ReconcileOperationalClaims();
                Require(adapter.HasCustomerLease, "Repeated lease did not work.");
                spatial.ResetTransientRuntimeStateAfterLoad();
                Require(!adapter.HasCustomerLease,
                    "The bar adapter reported a cached customer lease after BBSIS had cleared its actual leases for load.");
                coordinator.ReconcileOperationalClaims();
                Require(adapter.HasCustomerLease && spatial.CountLeases(BistroBuilderSpatialClaimKind.Seat) == 1,
                    "Automatic bar reconciliation did not rebuild the actual customer lease after a BBSIS runtime reset.");
                Require(bars.UnregisterSpot(spot) && !adapter.HasCustomerLease && spatial.ActiveLeaseCount == 0,
                    "Unregistering a bar spot left a customer lease in the operational coordinator.");
                Debug.Log("[SAVIC] DYNAMIC BAR OPERATIONAL COORDINATOR SELF-TEST - PASS: late native registration, automatic customer lease, logical release and unregister cleanup.");
            }
            finally
            {
                if (group != null) bars?.ReleaseGroup(group);
                adapter?.ReleaseCustomerLease();
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    if (objects[i] == null) continue;
                    var subject = objects[i].GetComponent<BistroBuilderSpatialSubject>();
                    if (subject != null) spatial?.UnregisterSubject(subject);
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        public static void RunAutomaticAssociationFromCommandLine()
        {
            var objects = new List<GameObject>();
            var item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            var seatContract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
            seatContract.ConfigureForEditor("bar_seat_candidate_test", BistroBuilderBarSeatBinding.SpatialFamilyId,
                BistroBuilderAdaptiveSpatialProxyMode.Simple, new[] { "seating.bar" });
            BistroBuilderSpatialInteractionService spatial = null;
            RestaurantPlaceableRegistry placeables = null;
            BistroBuilderBarSeatBinding seat = null;
            try
            {
                var services = Make("Automatic stool native services", objects);
                placeables = services.AddComponent<RestaurantPlaceableRegistry>();
                var bars = services.AddComponent<BistroBuilderBarServiceRegistry>();
                spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
                var assessment = services.AddComponent<BistroBuilderSpatialPlacementAssessmentService>();
                assessment.ConfigureForEditor(spatial);
                var barContract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(
                    SavicBarCounterFunctionAdapter.ContractPath);
                var root = Make("Automatic stool native bar", objects);
                root.transform.position = new Vector3(60, 0, 60);
                var customer = Node(root, "Customer", new Vector3(0, 0, 0.75f));
                var waiter = Node(root, "Waiter", new Vector3(0, 0, -0.75f));
                var counter = Node(root, "Surface", new Vector3(0, 1.05f, 0));
                var spot = root.AddComponent<BistroBuilderBarServiceSpot>();
                Require(spot.TryConfigure("bar.seat.auto.test", customer, waiter, 1, true) &&
                    spot.TryConfigureCounterSurface(counter), "Automatic bar fixture is invalid.");
                var proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
                proxy.AddPart(new BistroBuilderSpatialProxyPart { partId = "operational", layer = BistroBuilderSpatialProxyLayer.Operational,
                    localCenter = waiter.localPosition, size = Vector2.one * 0.2f });
                var subject = root.AddComponent<BistroBuilderSpatialSubject>();
                subject.Configure("spatial.bar.seat.auto.test", barContract, proxy);
                var adapter = root.AddComponent<BistroBuilderBarSpatialAdapter>(); adapter.Configure(spot, subject, spatial);
                Require(spatial.RegisterSubject(subject) && bars.TryRegisterSpot(spot, out _), "Automatic bar fixture registration failed.");
                assessment.RegisterProvider(adapter);
                seat = MakeSeat("seat_auto_test", new Vector3(70, 0, 70), item, seatContract, objects, placeables, bars, spatial);
                seat.EnableAutomaticAssociationForEditor(assessment);
                seat.GetComponent<BistroBuilderSpatialSubject>().Configure("spatial.bar.seat.template", seatContract,
                    seat.GetComponent<BistroBuilderAdaptiveSpatialProxy>());
                var originalPose = seat.transform.position;
                Require(seat.TryResolveSpotAtPose(customer.position, Quaternion.identity, out var proposed, out string error) &&
                    proposed == spot && seat.transform.position == originalPose && spot.AttachedSeat == null &&
                    spatial.SubjectCount == 1, "Candidate discovery moved or registered the provisional stool: " + error);
                var evaluation = assessment.EvaluateCandidate(seat.GetComponent<BistroBuilderSpatialSubject>(),
                    customer.position, Quaternion.identity, null, null);
                Require(evaluation.IsValid && seat.transform.position == originalPose, evaluation.TechnicalMessage);
                Require(!seat.TryResolveSpotAtPose(customer.position, Quaternion.Euler(0, 180, 0), out _, out _) &&
                    !seat.TryResolveSpotAtPose(customer.position + Vector3.right, Quaternion.identity, out _, out _) &&
                    !seat.TryResolveSpotAtPose(customer.position, new Quaternion(float.NaN, 0, 0, 1), out _, out _),
                    "Candidate pose validation accepted wrong facing, alignment or non-finite rotation.");
                var rule = services.AddComponent<BistroBuilderBarSeatPlacementConstraintRule>();
                var context = new RestaurantPlacementConstraintContext(seat.GetComponent<RestaurantAreaMember>(),
                    customer.position, Quaternion.identity, null, seat.GetComponent<RestaurantPlacementFootprint>(), null, null);
                Require(rule.Evaluate(context).IsValid, "The canonical seat placement rule rejected the compatible proposed pose.");
                seat.transform.position = customer.position;

                // The exact seat-bay relation must not authorize a waiter or transfer port.
                var savedWaiter = waiter.localPosition;
                waiter.position = seat.transform.position;
                evaluation = assessment.EvaluateCandidate(seat.GetComponent<BistroBuilderSpatialSubject>(),
                    seat.transform.position, seat.transform.rotation, null, null);
                Require(!evaluation.IsValid && !placeables.RegisterPlaceable(seat.GetComponent<RestaurantPlaceableObject>()) &&
                    !placeables.ContainsPlaceable(seat.GetComponent<RestaurantPlaceableObject>()) &&
                    spot.AttachedSeat == null && spatial.SubjectCount == 1,
                    "A seat-specific relationship exempted the bar's work/transfer port or retained a failed registration.");
                waiter.localPosition = savedWaiter;
                Require(placeables.RegisterPlaceable(seat.GetComponent<RestaurantPlaceableObject>()) &&
                    seat.AttachedSpot == spot && seat.ValidateRuntimeAssociation(out error) &&
                    seat.SpatialSubjectId == BistroBuilderBarSeatBinding.BuildSpatialId("seat_auto_test") &&
                    bars.FreeCapacity == 1 && spatial.SubjectCount == 2,
                    "Canonical registration did not derive the persisted ID and attach the native seat: " + error);
                Require(seat.TryCompleteActivation(out error) && bars.RegisteredSpotCount == 1 && spatial.SubjectCount == 2,
                    "Automatic seat activation was not idempotent: " + error);
                Require(placeables.UnregisterPlaceable(seat.GetComponent<RestaurantPlaceableObject>()) && spot.AttachedSeat == null &&
                    spatial.SubjectCount == 1, "Unregistering the stool left its body or bar association behind.");
                Require(placeables.RegisterPlaceable(seat.GetComponent<RestaurantPlaceableObject>()) &&
                    seat.ValidateRuntimeAssociation(out error), "Canonical reactivation did not rebuild its association: " + error);
                Debug.Log("[SAVIC] AUTOMATIC NATIVE BAR SEAT PLACEMENT - PASS: proposed pose without mutation, unique native association, " +
                    "persisted body identity, exact seat-bay relationship, protected work/transfer ports, failed-registration rollback, reactivation and cleanup.");
            }
            finally
            {
                if (seat != null) placeables?.UnregisterPlaceable(seat.GetComponent<RestaurantPlaceableObject>());
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    if (objects[i] == null) continue;
                    var subject = objects[i].GetComponent<BistroBuilderSpatialSubject>();
                    if (subject != null) spatial?.UnregisterSubject(subject);
                    Object.DestroyImmediate(objects[i]);
                }
                Object.DestroyImmediate(item); Object.DestroyImmediate(seatContract);
            }
        }

        public static void RunFromCommandLine()
        {
            List<GameObject> objects = new List<GameObject>();
            var item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            var seatContract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
            seatContract.ConfigureForEditor("bar_seat_test", BistroBuilderBarSeatBinding.SpatialFamilyId,
                BistroBuilderAdaptiveSpatialProxyMode.Simple, new [] { "customer_seating" });
            BistroBuilderBarSpatialAdapter barAdapter = null;
            BistroBuilderBarSeatBinding seat = null, duplicate = null;
            RestaurantPlaceableRegistry placeables = null;
            BistroBuilderSpatialInteractionService spatial = null;
            BistroBuilderBarServiceRegistry bars = null;
            BistroBuilderBarServiceSpot spot = null;
            CustomerGroup group = null;
            try
            {
                var barContract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(
                    "Assets/Resources/BistroBuilder/Spatial/Contracts/BB_SpatialContract_Bar_Service_Spot.asset");
                Require(barContract != null, "Missing native bar contract.");
                GameObject services = Make("Seat binding native services", objects);
                placeables = services.AddComponent<RestaurantPlaceableRegistry>();
                bars = services.AddComponent<BistroBuilderBarServiceRegistry>();
                spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
                GameObject barRoot = Make("Seat binding native bar", objects);
                Transform customer = Node(barRoot, "Customer", new Vector3(0, 0, 0.75f));
                Transform waiter = Node(barRoot, "Waiter", new Vector3(0, 0, -0.75f));
                Transform counter = Node(barRoot, "CounterSurface", new Vector3(0, 1.05f, 0));
                spot = barRoot.AddComponent<BistroBuilderBarServiceSpot>();
                Require(spot.TryConfigure("bar.seat.native.test", customer, waiter, 1, true) &&
                    spot.TryConfigureCounterSurface(counter), "Invalid native bar fixture.");
                var barProxy = barRoot.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
                barProxy.AddPart(new BistroBuilderSpatialProxyPart { partId = "operational", layer = BistroBuilderSpatialProxyLayer.Operational,
                    localCenter = waiter.localPosition, size = new Vector2(0.2f, 0.2f) });
                var barSubject = barRoot.AddComponent<BistroBuilderSpatialSubject>();
                barSubject.Configure("spatial.bar.seat.native.test", barContract, barProxy);
                barAdapter = barRoot.AddComponent<BistroBuilderBarSpatialAdapter>();
                barAdapter.Configure(spot, barSubject, spatial);
                string error;
                Require(spatial.RegisterSubject(barSubject) && bars.TryRegisterSpot(spot, out error), "Native registration failed.");
                int originalCapacity = bars.FreeCapacity;

                seat = MakeSeat("seat_binding_a", customer.position, item, seatContract, objects, placeables, bars, spatial);
                Require(seat.ValidateForSpot(spot, out error), error);
                Require(!seat.TryAttach(spot, out _) && !spatial.TryGetSubject(seat.SpatialSubjectId, out _),
                    "A provisional stool became an operational seat/body.");
                Confirm(seat, placeables, spatial);
                Require(!barAdapter.TryAcquireCustomerLease(MakeGroup(99919, objects), out _),
                    "An unattached physical stool did not block the original standing lease.");
                Require(seat.TryAttach(spot, out error) && seat.ValidateRuntimeAssociation(out error), error);
                Require(seat.TryAttach(spot, out error) && bars.FreeCapacity == originalCapacity && bars.RegisteredSpotCount == 1,
                    "Seat association was not idempotent or created capacity.");
                var barBinding = barRoot.AddComponent<BistroBuilderBarPlaceableBinding>();
                barBinding.ConfigureForEditor(new [] { spot }, barContract, placeables, bars, spatial);
                Require(!barBinding.CanDeactivate(out _), "Removing the bar could orphan an attached stool.");
                Require(spot.CustomerApproachPoint == seat.ApproachFrame && spot.CustomerPoint == customer &&
                    spot.CustomerApproachPoint.position.y == customer.position.y && seat.SeatFrame.position.y > customer.position.y,
                    "Floor Navigation approach was confused with the elevated seat frame or original service port.");
                Require(!spot.TryConfigure("replacement_bar_identity", customer, waiter, 1, true) &&
                    !spot.TryConfigureCounterSurface(Node(barRoot, "OtherSurface", new Vector3(0, 1.2f, 0))),
                    "An attached seat allowed the bar identity or physical surface to change.");

                duplicate = MakeSeat("seat_binding_b", customer.position, item, seatContract, objects, placeables, bars, spatial);
                Confirm(duplicate, placeables, spatial);
                Require(!duplicate.TryAttach(spot, out _), "Two stools attached to one native place.");
                Require(spatial.TryGetSubject(duplicate.SpatialSubjectId, out var duplicateSubject), "Duplicate test body missing.");
                spatial.UnregisterSubject(duplicateSubject);
                group = MakeGroup(99920, objects);
                Require(bars.TryAllocateSpot(group, BistroBuilderServiceMode.WaitingAtBar, out var allocated) && allocated == spot &&
                    seat.Occupant == group && bars.FreeCapacity == 0, "Seat occupancy did not derive from the native bar allocation.");
                Require(!seat.TryDetach(out _) && !seat.CanDeactivate(out _) && seat.TryAttach(spot, out error),
                    "Occupied association detached or could not be validated idempotently.");
                Require(barAdapter.TryAcquireCustomerLease(group, out error) && spatial.ActiveLeaseCount == 1, error);
                Require(!barAdapter.TryAcquireCustomerLease(MakeGroup(99921, objects), out _), "A foreign group acquired the seated customer's lease.");
                Require(bars.ReleaseGroup(group) && seat.Occupant == null && !seat.TryDetach(out _) && !seat.CanDeactivate(out _),
                    "Logical release ignored a remaining spatial lease.");
                barAdapter.ReleaseCustomerLease();

                // Related geometry exclusion is specific to the attached body: an unrelated blocker still wins.
                spatial.RegisterSubject(duplicateSubject);
                Require(bars.TryAllocateSpot(group, BistroBuilderServiceMode.WaitingAtBar, out _) &&
                    !barAdapter.TryAcquireCustomerLease(group, out _), "Seat association ignored an unrelated physical body.");
                spatial.UnregisterSubject(duplicateSubject);
                Require(barAdapter.TryGetPortVolume(BistroBuilderBarSpatialAdapter.CustomerPortId, out var volume, out var mode), "Customer port missing.");
                var request = new BistroBuilderSpatialClaimRequest { ownerId = "foreign-seat-owner", subjectId = barSubject.SubjectId,
                    portId = BistroBuilderBarSpatialAdapter.CustomerPortId, kind = BistroBuilderSpatialClaimKind.Seat,
                    conflictMode = mode, volume = volume, priority = 80, validateAgainstStaticGeometry = false };
                Require(spatial.TryAcquireLease(request, out var foreignLease, out _) &&
                    !barAdapter.TryAcquireCustomerLease(group, out _), "An attached body bypassed an unrelated spatial lease.");
                spatial.ReleaseLease(foreignLease.leaseId);
                Require(bars.ReleaseGroup(group), "Native release failed.");

                seat.transform.rotation = Quaternion.Euler(0, 180f, 0);
                Require(!seat.ValidateRuntimeAssociation(out _) && !bars.TryAllocateSpot(group, out _), "Wrong-facing stool remained allocatable.");
                seat.transform.rotation = Quaternion.identity;
                counter.localPosition = new Vector3(0, 0.9f, 0);
                Require(!seat.ValidateRuntimeAssociation(out _), "Incompatible counter/seat height was accepted.");
                counter.localPosition = new Vector3(0, 1.05f, 0);
                seat.ApproachFrame.localPosition = Vector3.zero;
                Require(!seat.ValidateRuntimeAssociation(out _), "A floor approach inside the stool body was accepted.");
                seat.ApproachFrame.localPosition = new Vector3(0, 0, -0.65f);
                seat.GetComponent<BistroBuilderAdaptiveSpatialProxy>().Parts[0].size = new Vector2(float.NaN, 0.4f);
                Require(!seat.ValidateRuntimeAssociation(out _), "Non-finite physical seat geometry was accepted.");
                seat.GetComponent<BistroBuilderAdaptiveSpatialProxy>().Parts[0].size = new Vector2(0.4f, 0.4f);
                Require(seat.TryDetach(out error) && spot.CustomerApproachPoint == customer && bars.FreeCapacity == originalCapacity, error);
                Require(spot.TryConfigure(spot.BarSpotId, customer, waiter, 2, true) && !seat.TryAttach(spot, out _),
                    "A single physical stool was associated with a multi-person native spot.");
                Require(spot.TryConfigure(spot.BarSpotId, customer, waiter, 1, true), "Capacity reset failed.");
                spatial.UnregisterSubject(seat.GetComponent<BistroBuilderSpatialSubject>());
                Require(!seat.TryAttach(spot, out _), "A missing BBSIS stool body was treated as confirmed.");
                Debug.Log("[SAVIC] BAR SEAT BINDING SELF-TEST - PASS: native one-to-one association, provisional isolation, " +
                    "unchanged capacity, distinct floor/seat frames, native occupancy, busy guards, exact related body, " +
                    "foreign body/lease rejection, invalid facing/height/approach/data and cleanup. No stool published.");
            }
            finally
            {
                barAdapter?.ReleaseCustomerLease();
                if (group != null) bars?.ReleaseGroup(group);
                seat?.TryDetach(out _); duplicate?.TryDetach(out _);
                if (spot != null) bars?.UnregisterSpot(spot);
                for (int i = objects.Count - 1; i >= 0; i--)
                {
                    if (objects[i] == null) continue;
                    var subject = objects[i].GetComponent<BistroBuilderSpatialSubject>();
                    if (subject != null) spatial?.UnregisterSubject(subject);
                    Object.DestroyImmediate(objects[i]);
                }
                Object.DestroyImmediate(item); Object.DestroyImmediate(seatContract);
            }
        }
        private static BistroBuilderBarSeatBinding MakeSeat(string id, Vector3 position, RestaurantPlaceableItemDefinition item,
            BistroBuilderSpatialContractDefinition contract, List<GameObject> objects, RestaurantPlaceableRegistry placeables,
            BistroBuilderBarServiceRegistry bars, BistroBuilderSpatialInteractionService spatial)
        {
            GameObject root = Make(id, objects); root.transform.position = position;
            var placeable = root.AddComponent<RestaurantPlaceableObject>(); placeable.SetItemDefinition(item); placeable.AssignInstanceId(id);
            var proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.AddPart(new BistroBuilderSpatialProxyPart { partId = "body", size = new Vector2(0.4f, 0.4f) });
            root.GetComponent<RestaurantPlacementFootprint>().ConfigureRuntime(Vector3.zero, new Vector2(0.4f, 0.4f), 0f);
            root.AddComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
            var subject = root.AddComponent<BistroBuilderSpatialSubject>();
            var seat = root.AddComponent<BistroBuilderBarSeatBinding>();
            seat.ConfigureForEditor(Node(root, "Seat", new Vector3(0, 0.75f, 0)),
                Node(root, "Approach", new Vector3(0, 0, -0.65f)), placeables, bars, spatial);
            subject.Configure(BistroBuilderBarSeatBinding.BuildSpatialId(id), contract, proxy);
            return seat;
        }
        private static void Confirm(BistroBuilderBarSeatBinding seat, RestaurantPlaceableRegistry placeables, BistroBuilderSpatialInteractionService spatial)
        {
            Require(placeables.RegisterPlaceable(seat.GetComponent<RestaurantPlaceableObject>()) &&
                spatial.RegisterSubject(seat.GetComponent<BistroBuilderSpatialSubject>()), "Confirmed seat registration failed.");
        }
        private static CustomerGroup MakeGroup(int id, List<GameObject> objects)
        {
            var group = Make("Seat group " + id, objects).AddComponent<CustomerGroup>();
            Require(group.Initialize(id, 1, BistroBuilderServiceMode.WaitingAtBar), "Native customer initialization failed."); return group;
        }
        private static Transform Node(GameObject root, string name, Vector3 position)
        { var node = new GameObject(name).transform; node.SetParent(root.transform, false); node.localPosition = position; return node; }
        private static GameObject Make(string name, List<GameObject> objects)
        { var root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; objects.Add(root); return root; }
        private static void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException(error); }
    }
}
