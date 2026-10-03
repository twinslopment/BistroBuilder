using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicElevatedSpatialSelfTest
    {
        public static void RunFromCommandLine()
        {
            var high = BistroBuilderSpatialVolume.Box(new Vector3(0f, 2.45f, 0f), Vector3.right, Vector3.forward, Vector2.one)
                .WithHeightRange(BistroBuilderSpatialHeightRange.Between(2.2f, 2.7f));
            var low = BistroBuilderSpatialVolume.Circle(Vector3.zero, 0.3f)
                .WithHeightRange(BistroBuilderSpatialHeightRange.Between(0f, 2f));
            Require(!high.Overlaps(low) && !low.Overlaps(high), "Vertically separated volumes conflict.");
            Require(high.Overlaps(low.WithHeightRange(BistroBuilderSpatialHeightRange.Between(0f, 2.3f))), "Partial vertical overlap was lost.");
            Require(high.Overlaps(high), "Equal-height physical overlap was lost.");
            Require(high.Overlaps(low.WithHeightRange(default)), "Unknown height released an obstacle.");
            Require(high.Overlaps(low.WithHeightRange(BistroBuilderSpatialHeightRange.Between(float.NaN, 2f))), "Invalid height released an obstacle.");
            Require(!high.ContainsPoint(Vector3.zero) && high.ContainsPoint(new Vector3(0f, 2.4f, 0f)), "Volume containment ignored height.");
            Require(!high.Overlaps(low.WithHeightRange(BistroBuilderSpatialHeightRange.Between(0f, 2.2f))), "Touching intervals were treated as physical penetration.");
            var first = Shape(high);
            var second = Shape(low);
            Require(RestaurantPlacementCollisionUtility.EvaluateConflict(first, second) == RestaurantPlacementConflictType.None,
                "Placement differs from BBSIS vertical separation.");
            Require(RestaurantPlacementCollisionUtility.EvaluateConflict(first, Shape(high)) == RestaurantPlacementConflictType.PhysicalOverlap,
                "Placement lost equal-height overlap.");
            Require(RestaurantPlacementCollisionUtility.EvaluateConflict(first, new RestaurantPlacementShape(Vector3.zero,
                Vector3.right, Vector3.forward, Vector2.one, 0f)) == RestaurantPlacementConflictType.PhysicalOverlap,
                "Legacy footprint ceased blocking conservatively.");
            var clearance = new RestaurantPlacementShape(low.center, Vector3.right, Vector3.forward, Vector2.one, 0.3f, low.heightRange);
            Require(RestaurantPlacementCollisionUtility.EvaluateConflict(first, clearance) == RestaurantPlacementConflictType.MinimumClearanceViolation,
                "Vertical clearance was not preserved.");
            Debug.Log("[SAVIC] ELEVATED SPATIAL PRIMITIVES - PASS: separation, overlap, touching, clearance, unknown/invalid conservative and containment.");
        }

        public static void VerifyNativeFromCommandLine()
        {
            Require(Application.isBatchMode, "Native isolated probe is batch-only.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RunFromCommandLine();
            var objects = new List<GameObject>();
            var definition = ScriptableObject.CreateInstance<RestaurantAreaDefinition>();
            var contract = ScriptableObject.CreateInstance<BistroBuilderSpatialContractDefinition>();
            try
            {
                GameObject services = New("Elevated probe services", objects);
                var spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
                contract.ConfigureForEditor("test.elevated", "test.elevated", BistroBuilderAdaptiveSpatialProxyMode.Simple, null);
                GameObject body = Body("Overhead body", 2.2f, 0.5f, objects);
                var subject = body.AddComponent<BistroBuilderSpatialSubject>();
                subject.Configure("test.elevated.body", contract, body.GetComponent<BistroBuilderAdaptiveSpatialProxy>());
                Require(spatial.RegisterSubject(subject) || spatial.TryGetSubject(subject.SubjectId, out _), "Native elevated subject failed registration.");
                var lower = BistroBuilderSpatialVolume.Circle(Vector3.zero, 0.28f)
                    .WithHeightRange(BistroBuilderSpatialHeightRange.Between(0f, 2f));
                Require(!spatial.TryFindStaticGeometryConflict(lower, "", "", out _), "BBSIS blocked the certified lower interval.");
                Require(spatial.TryFindStaticGeometryConflict(lower.WithHeightRange(default), "", "", out _), "BBSIS released an unknown claim.");
                Require(spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "test.lower.person",
                    volume = lower, validateAgainstStaticGeometry = true }, out BistroBuilderSpatialLease lease, out _),
                    "Native bounded lease below elevated body was rejected.");
                Require(spatial.ReleaseLease(lease.leaseId), "Native lower lease did not clean up.");
                Require(!spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "test.intersection",
                    volume = lower.WithHeightRange(BistroBuilderSpatialHeightRange.Between(0f, 2.4f)), validateAgainstStaticGeometry = true },
                    out _, out _), "BBSIS accepted head penetration into the elevated body.");

                GameObject areaObject = New("Elevated test area", objects);
                var bounds = areaObject.AddComponent<BoxCollider>(); bounds.size = new Vector3(12f, 8f, 12f); bounds.isTrigger = true;
                var area = areaObject.AddComponent<RestaurantArea>(); Set(area, "areaId", "elevated_probe"); Set(area, "definition", definition);
                var areaData = new SerializedObject(area); areaData.FindProperty("boundaryColliders").arraySize = 1;
                areaData.FindProperty("boundaryColliders").GetArrayElementAtIndex(0).objectReferenceValue = bounds;
                areaData.ApplyModifiedPropertiesWithoutUndo();
                var areas = services.AddComponent<RestaurantAreaRegistry>(); Require(areas.RegisterArea(area), "Area registration failed.");
                var members = services.AddComponent<RestaurantAreaMemberRegistry>();
                var assignment = services.AddComponent<RestaurantAreaAssignmentService>(); Set(assignment, "areaRegistry", areas);
                var placements = services.AddComponent<RestaurantPlacementRegistry>(); Set(placements, "memberRegistry", members);
                var obstacles = services.AddComponent<RestaurantPlacementObstacleRegistry>();
                var validation = services.AddComponent<RestaurantPlacementValidationService>();
                Set(validation, "areaAssignmentService", assignment); Set(validation, "placementRegistry", placements); Set(validation, "obstacleRegistry", obstacles);
                var bodyMember = body.AddComponent<RestaurantAreaMember>(); bodyMember.SetArea(area); members.RegisterMember(bodyMember);
                var footprint = body.GetComponent<RestaurantPlacementFootprint>();
                Require(placements.ContainsFootprint(footprint) || placements.RegisterFootprint(footprint), "Body footprint registration failed.");
                GameObject candidate = Body("Lower physical furniture", 0f, 1f, objects);
                var member = candidate.AddComponent<RestaurantAreaMember>(); member.SetArea(area);
                Physics.SyncTransforms();
                Require(validation.ValidatePlacement(member, Vector3.zero, Quaternion.identity).IsValid, "Native placement blocked nonintersecting heights.");
                Require(validation.ValidatePlacement(member, new Vector3(0f, 1.5f, 0f), Quaternion.identity).Status == RestaurantPlacementValidationStatus.PhysicalOverlap,
                    "Candidate height translation failed to detect actual overlap.");
                Require(validation.ValidatePlacement(bodyMember, new Vector3(5.9f, 0f, 0f), Quaternion.identity).Status == RestaurantPlacementValidationStatus.FootprintOutsideCandidateArea,
                    "Elevation bypassed XZ area limits.");
                var assessment = services.AddComponent<BistroBuilderSpatialPlacementAssessmentService>();
                assessment.ConfigureForEditor(spatial);
                var lowerWorkZone = candidate.AddComponent<SavicElevatedSpatialProbeSemantics>();
                lowerWorkZone.heightRange = BistroBuilderSpatialHeightRange.Between(0f, 2f);
                assessment.RegisterProvider(lowerWorkZone);
                Require(assessment.EvaluateCandidate(subject, Vector3.zero, Quaternion.identity, null, null).IsValid,
                    "Preflight blocked a certified lower work zone.");
                Require(!assessment.EvaluateCandidate(subject, new Vector3(0f, -1f, 0f), Quaternion.identity, null, null).IsValid,
                    "Preflight did not translate physical height into the candidate pose.");
                Require(assessment.EvaluateCandidate(subject, new Vector3(4f, -1f, 0f), Quaternion.Euler(0f, 90f, 0f), null, null).IsValid &&
                    body.transform.position == Vector3.zero,
                    "Preflight pose evaluation mutated the body or lost horizontal separation.");
                lowerWorkZone.heightRange = default;
                Require(!assessment.EvaluateCandidate(subject, Vector3.zero, Quaternion.identity, null, null).IsValid,
                    "Preflight released an operational zone with unknown height.");
                lowerWorkZone.heightRange = BistroBuilderSpatialHeightRange.Between(0f, 2f);
                var lowerSubject = candidate.AddComponent<BistroBuilderSpatialSubject>();
                lowerSubject.Configure("test.lower.candidate", contract, candidate.GetComponent<BistroBuilderAdaptiveSpatialProxy>());
                Require(assessment.EvaluateCandidate(lowerSubject, Vector3.zero, Quaternion.identity, null, null).IsValid,
                    "Certified lower candidate semantic volume was blocked.");
                Require(!assessment.EvaluateCandidate(lowerSubject, new Vector3(0f, 1f, 0f), Quaternion.identity, null, null).IsValid &&
                    candidate.transform.position == Vector3.zero,
                    "Preflight did not translate candidate semantic height without moving its Transform.");
                assessment.UnregisterProvider(lowerWorkZone);
                var shapes = new List<RestaurantPlacementShape>();
                body.transform.localScale = new Vector3(2f, 3f, 2f);
                Require(BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(footprint, new Vector3(4f, 1f, 2f), Quaternion.Euler(0f, 90f, 0f), shapes, out _) &&
                    shapes.Count == 1 && Mathf.Abs(shapes[0].HeightRange.minimum - 7.6f) < 0.0001f && body.transform.position == Vector3.zero,
                    "Candidate scale/rotation/translation did not preserve metric height or mutated Transform.");
                body.transform.localScale = Vector3.one;
                var part = body.GetComponent<BistroBuilderAdaptiveSpatialProxy>().Parts[0];
                part.height = float.NaN;
                Require(!BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(footprint, Vector3.zero, Quaternion.identity, shapes, out _) &&
                    !shapes[0].HeightRange.IsBounded && body.GetComponent<BistroBuilderAdaptiveSpatialProxy>().BuildWorldVolumes(BistroBuilderSpatialProxyLayer.Static,
                    new List<BistroBuilderSpatialVolume>()) == 1, "Invalid vertical extent did not fall back conservatively.");
                Require(validation.ValidatePlacement(bodyMember, Vector3.zero, Quaternion.identity).Status == RestaurantPlacementValidationStatus.SystemUnavailable,
                    "Native candidate accepted invalid height.");
                part.height = 0.5f;
                // The lower object only served the placement checks. Keep the real elevated obstacle in topology.
                Object.DestroyImmediate(candidate); objects.Remove(candidate);
                var navigation = services.AddComponent<BistroBuilderNavigationService>(); navigation.RebuildNavigationTopology();
                var points = new List<Vector3>();
                Vector3 origin = new Vector3(0f, 0f, -4f), target = new Vector3(0f, 0f, 4f);
                float length = 0f;
                BistroBuilderNavigationRouteKind kind = default;
                Require(navigation.StaticObstacleCount == 1 && navigation.TryBuildRoute("test.lower.walker", BistroBuilderNavigationAgentMask.Waiter,
                    origin, target, 0.28f, points, out length, out kind) && kind != BistroBuilderNavigationRouteKind.DirectDegraded,
                    "Native navigation did not produce a route below the body.");
                float minimumDistance = float.MaxValue;
                Vector3 previous = origin;
                var volume = part.BuildWorldVolume(body.transform);
                foreach (Vector3 next in points)
                {
                    int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, next) / 0.05f));
                    for (int sample = 0; sample <= samples; sample++)
                    {
                        Vector3 point = Vector3.Lerp(previous, next, sample / (float)samples);
                        minimumDistance = Mathf.Min(minimumDistance, new Vector2(point.x, point.z).magnitude);
                        Require(!volume.Overlaps(BistroBuilderSpatialVolume.Circle(point, 0.28f).WithHeightRange(
                            BistroBuilderSpatialHeightRange.Between(point.y, point.y + 2f))), "Route's human envelope penetrates elevated body.");
                    }
                    previous = next;
                }
                Require(minimumDistance < 0.25f, "Route went around the body; it did not prove an underpass.");
                part.localCenter = new Vector3(0f, 1.95f, 0f); navigation.RebuildNavigationTopology();
                Require(!navigation.TryBuildRoute("test.low.ceiling", BistroBuilderNavigationAgentMask.Waiter, origin, Vector3.zero,
                    0.28f, points, out _, out _), "Endpoint/docking bypassed insufficient vertical clearance.");
                part.localCenter = new Vector3(0f, 2.45f, 0f); part.height = float.NaN; navigation.RebuildNavigationTopology();
                Require(!navigation.TryBuildRoute("test.unknown.ceiling", BistroBuilderNavigationAgentMask.Waiter, origin, Vector3.zero,
                    0.28f, points, out _, out _), "Invalid height left a traversable endpoint.");
                Debug.Log("[SAVIC] ELEVATED NATIVE SPATIAL - PASS: BBSIS body/lease, placement overlap/limits/pose, conservative invalid/unknown, human underpass " +
                    kind + " " + length.ToString("F3") + "m sampled 5cm, low endpoint rejected, cleanup.");
            }
            finally { for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]); Object.DestroyImmediate(definition); Object.DestroyImmediate(contract); }
        }

        public static void VerifyCanonicalFromCommandLine()
        {
            VerifyNativeFromCommandLine();
            SavicBarSeatBindingSelfTest.VerifyNativeAndCanonicalFromCommandLine();
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
        }

        private static GameObject Body(string name, float bottom, float height, List<GameObject> objects)
        {
            GameObject root = New(name, objects);
            root.AddComponent<RestaurantPlacementFootprint>().ConfigureRuntime(new Vector3(0f, bottom + height * 0.5f, 0f), new Vector2(2f, 1f));
            var proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.AddPart(new BistroBuilderSpatialProxyPart { partId = "body", localCenter = new Vector3(0f, bottom + height * 0.5f, 0f),
                size = new Vector2(2f, 1f), hasVerticalExtent = true, height = height });
            root.AddComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
            return root;
        }
        private static GameObject New(string name, List<GameObject> objects) { var root = new GameObject(name); objects.Add(root); return root; }
        private static RestaurantPlacementShape Shape(BistroBuilderSpatialVolume volume) => new RestaurantPlacementShape(volume.center,
            volume.rightAxis, volume.forwardAxis, Vector2.one, 0f, volume.heightRange);
        private static void Set(Object target, string name, Object value) { var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Set(Object target, string name, string value) { var data = new SerializedObject(target); data.FindProperty(name).stringValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Require(bool result, string error) { if (!result) throw new InvalidOperationException(error); }
    }

    // Editor-only fixture exercising the native candidate preflight, without
    // assigning heights to any production operational zone.
    public sealed class SavicElevatedSpatialProbeSemantics : MonoBehaviour, IBistroBuilderSpatialSemanticProvider
    {
        public BistroBuilderSpatialHeightRange heightRange;
        public string SpatialSubjectId => GetComponent<BistroBuilderSpatialSubject>()?.SubjectId ?? "test.lower.workzone";
        public int WriteSemanticVolumes(List<BistroBuilderSpatialSemanticVolume> results)
        {
            results.Add(new BistroBuilderSpatialSemanticVolume { subjectId = SpatialSubjectId, semanticId = "workzone",
                role = BistroBuilderSpatialSemanticRole.WorkZone, layer = BistroBuilderSpatialProxyLayer.Operational,
                conflictMode = BistroBuilderSpatialConflictMode.Block, critical = true,
                volume = BistroBuilderSpatialVolume.Circle(transform.position, 0.28f).WithHeightRange(heightRange.Shifted(transform.position.y)) });
            return 1;
        }
    }
}
