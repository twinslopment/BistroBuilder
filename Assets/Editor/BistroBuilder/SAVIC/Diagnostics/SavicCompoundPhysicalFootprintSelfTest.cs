using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicCompoundPhysicalFootprintSelfTest
    {
        public static void RunFromCommandLine()
        {
            GameObject root = MakeBody(true);
            GameObject simple = new GameObject("Simple footprint test") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                RestaurantPlacementFootprint footprint = root.GetComponent<RestaurantPlacementFootprint>();
                BistroBuilderAdaptiveSpatialProxy proxy = root.GetComponent<BistroBuilderAdaptiveSpatialProxy>();
                BistroBuilderSpatialPhysicalFootprintAdapter adapter = root.GetComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
                List<RestaurantPlacementShape> shapes = new List<RestaurantPlacementShape>();
                List<RestaurantPlacementShape> probe = new List<RestaurantPlacementShape>();
                Require(Write(footprint, shapes) && shapes.Count == 3, "Root BBSIS body did not project its three static boxes.");
                Probe(probe, Vector3.zero, 0.2f);
                Require(BistroBuilderPhysicalPlacementGeometry.EvaluateConflict(shapes, probe) == RestaurantPlacementConflictType.None,
                    "U-shaped body's empty interior was blocked.");
                Probe(probe, new Vector3(1.75f, 0f, 0f), 0.2f);
                Require(BistroBuilderPhysicalPlacementGeometry.EvaluateConflict(shapes, probe) == RestaurantPlacementConflictType.PhysicalOverlap,
                    "Real side wall did not block placement.");
                Probe(probe, new Vector3(1.23f, 0f, 0f), 0.2f, 0.1f);
                Require(BistroBuilderPhysicalPlacementGeometry.EvaluateConflict(shapes, probe) == RestaurantPlacementConflictType.MinimumClearanceViolation,
                    "Compound body ignored minimum clearance.");
                Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);
                Vector3 position = new Vector3(8f, 0f, -6f);
                root.transform.localScale = new Vector3(2f, 1f, 3f);
                Require(BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(footprint, position, rotation, shapes, out _) &&
                    Vector3.Distance(shapes[0].Center, position + rotation * new Vector3(-3.5f, 0f, 0f)) < 0.0001f &&
                    Vector2.Distance(shapes[0].HalfExtents, new Vector2(0.5f, 6f)) < 0.0001f && root.transform.position == Vector3.zero,
                    "Candidate pose/metric scale altered the root or projected the wrong body.");
                root.transform.localScale = Vector3.one;
                proxy.Parts[1].localCenter = new Vector3(float.NaN, 0f, 0f);
                Require(!Write(footprint, shapes) && shapes.Count == 1 && shapes[0].HalfWidth == 2f,
                    "Invalid part exposed a partial body instead of its conservative envelope.");
                proxy.Parts[1].localCenter = new Vector3(1.75f, 0f, 0f);
                proxy.Parts[1].size = new Vector2(2f, 4f);
                Require(!Write(footprint, shapes), "Part outside the area envelope was accepted.");
                proxy.Parts[1].size = new Vector2(0.5f, 4f);
                proxy.Parts[1].anchor = simple.transform;
                Require(!Write(footprint, shapes), "Foreign or articulated geometry was accepted as a root static body.");
                proxy.Parts[1].anchor = null;
                proxy.Parts[1].shapeKind = BistroBuilderSpatialShapeKind.Circle;
                Require(!Write(footprint, shapes), "Unsupported body shape silently became a rectangle.");
                proxy.Parts[1].shapeKind = BistroBuilderSpatialShapeKind.OrientedBox;
                Require(!BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(footprint, Vector3.zero,
                    Quaternion.Euler(20f, 0f, 0f), shapes, out _), "Tilted 2D physical body was accepted.");
                root.transform.localScale = new Vector3(-1f, 1f, 1f);
                Require(!Write(footprint, shapes), "Reflected scale was accepted.");
                root.transform.localScale = Vector3.one;
                for (int i = 3; i <= BistroBuilderSpatialPhysicalFootprintAdapter.MaximumStaticParts; i++)
                    proxy.AddPart(Part("extra_" + i, Vector3.zero, Vector2.one));
                Require(!Write(footprint, shapes), "Unbounded proxy projection was accepted.");
                adapter.enabled = false;
                Require(Write(footprint, shapes) && shapes.Count == 1, "Disabled opt-in did not retain the legacy rectangle.");
                RestaurantPlacementFootprint simpleFootprint = simple.AddComponent<RestaurantPlacementFootprint>();
                simpleFootprint.ConfigureRuntime(new Vector3(0.2f, 0f, 0.3f), new Vector2(2f, 3f), 0.1f);
                Require(Write(simpleFootprint, shapes) && shapes.Count == 1 &&
                    Vector3.Distance(shapes[0].Center, simpleFootprint.BuildCurrentShape().Center) < 0.0001f &&
                    shapes[0].MinimumClearance == 0.1f, "Legacy footprint behavior changed.");
                Debug.Log("[SAVIC] COMPOUND PHYSICAL FOOTPRINT SELF-TEST - PASS: cavity, walls, clearance, candidate pose, scale, invalid data, bounded projection and legacy rectangle.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(simple); }
        }

        public static void VerifyCanonicalRegressionsFromCommandLine()
        {
            Require(Application.isBatchMode, "Isolated scene probe is batch-only.");
            SavicV1ClosureGate.RunFromMenu();
            VerifyNativeRegressionsFromCommandLine();
            Debug.Log("[SAVIC] COMPOUND PHYSICAL CANONICAL REGRESSIONS - PASS: closure gate, native placement and route, installed navigation and edit mode core.");
        }

        public static void VerifyNativeRegressionsFromCommandLine()
        {
            Require(Application.isBatchMode, "Isolated scene probe is batch-only.");
            RunFromCommandLine();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VerifyNativePlacementAndRoute();
            // The core projection test rebuilds all BBSIS subjects; it requires an empty scene.
            BistroBuilderEditBlock18CoreSelfTest.RunFromMenu();
            BistroBuilderNavigation17SelfTest.Run();
            Require(BistroBuilderNavigation17SelfTest.LastFailed == 0, "Installed Navigation 17 regression failed.");
            Debug.Log("[SAVIC] COMPOUND PHYSICAL NATIVE REGRESSIONS - PASS: native placement and route, installed navigation and edit mode core.");
        }

        private static void VerifyNativePlacementAndRoute()
        {
            GameObject root = MakeBody(false);
            GameObject areaObject = new GameObject("Compound route test area");
            GameObject services = new GameObject("Compound native test services");
            GameObject candidate = new GameObject("Compound placement probe");
            RestaurantAreaDefinition definition = ScriptableObject.CreateInstance<RestaurantAreaDefinition>();
            try
            {
                BoxCollider bounds = areaObject.AddComponent<BoxCollider>();
                bounds.size = new Vector3(16f, 2f, 16f);
                bounds.isTrigger = true;
                RestaurantArea area = areaObject.AddComponent<RestaurantArea>();
                Set(area, "areaId", "compound_probe");
                Set(area, "definition", definition);
                SerializedObject areaData = new SerializedObject(area);
                areaData.FindProperty("boundaryColliders").arraySize = 1;
                areaData.FindProperty("boundaryColliders").GetArrayElementAtIndex(0).objectReferenceValue = bounds;
                areaData.ApplyModifiedPropertiesWithoutUndo();
                RestaurantAreaRegistry areas = services.AddComponent<RestaurantAreaRegistry>();
                Require(areas.RegisterArea(area), "Native area could not be registered.");
                RestaurantAreaMemberRegistry members = services.AddComponent<RestaurantAreaMemberRegistry>();
                RestaurantAreaAssignmentService assignment = services.AddComponent<RestaurantAreaAssignmentService>();
                Set(assignment, "areaRegistry", areas);
                RestaurantPlacementRegistry placements = services.AddComponent<RestaurantPlacementRegistry>();
                Set(placements, "memberRegistry", members);
                RestaurantPlacementObstacleRegistry obstacles = services.AddComponent<RestaurantPlacementObstacleRegistry>();
                RestaurantPlacementValidationService validation = services.AddComponent<RestaurantPlacementValidationService>();
                Set(validation, "areaAssignmentService", assignment);
                Set(validation, "placementRegistry", placements);
                Set(validation, "obstacleRegistry", obstacles);
                RestaurantAreaMember bodyMember = root.AddComponent<RestaurantAreaMember>();
                bodyMember.SetArea(area);
                RestaurantPlacementFootprint body = root.GetComponent<RestaurantPlacementFootprint>();
                Require(members.RegisterMember(bodyMember) &&
                    (placements.ContainsFootprint(body) || placements.RegisterFootprint(body)),
                    "Native body footprint registration failed.");
                RestaurantAreaMember member = candidate.AddComponent<RestaurantAreaMember>();
                member.SetArea(area);
                RestaurantPlacementFootprint probe = candidate.AddComponent<RestaurantPlacementFootprint>();
                probe.ConfigureRuntime(Vector3.zero, new Vector2(0.4f, 0.4f));
                Physics.SyncTransforms();
                Require(validation.ValidatePlacement(member, Vector3.zero, Quaternion.identity).IsValid,
                    "Native placement validator blocked the compound cavity.");
                Require(validation.ValidatePlacement(member, new Vector3(1.75f, 0f, 0f), Quaternion.identity).Status ==
                    RestaurantPlacementValidationStatus.PhysicalOverlap, "Native placement validator ignored a real wall.");
                Require(validation.ValidatePlacement(bodyMember, new Vector3(7f, 0f, 0f), Quaternion.identity).Status ==
                    RestaurantPlacementValidationStatus.FootprintOutsideCandidateArea,
                    "Compound geometry bypassed coarse area boundary containment.");
                probe.ConfigureRuntime(Vector3.zero, Vector2.one, 0f, false);
                BistroBuilderNavigationService navigation = services.AddComponent<BistroBuilderNavigationService>();
                navigation.RebuildNavigationTopology();
                Require(navigation.StaticObstacleCount == 3, "Navigation did not consume the exact root BBSIS static parts.");
                List<Vector3> points = new List<Vector3>();
                Vector3 origin = new Vector3(0f, 0f, -5f);
                Require(navigation.TryBuildRoute("compound_waiter", BistroBuilderNavigationAgentMask.Waiter,
                    origin, Vector3.zero, 0.28f, points, out float length, out BistroBuilderNavigationRouteKind kind) &&
                    kind != BistroBuilderNavigationRouteKind.DirectDegraded && points.Count > 0 && length > 0f,
                    "Native navigation did not provide a real route through the U opening.");
                List<RestaurantPlacementShape> bodyShapes = new List<RestaurantPlacementShape>();
                List<RestaurantPlacementShape> agentShape = new List<RestaurantPlacementShape>();
                Require(Write(body, bodyShapes), "Native route body became invalid.");
                Vector3 previous = origin;
                foreach (Vector3 next in points)
                {
                    int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, next) / 0.05f));
                    for (int sample = 0; sample <= samples; sample++)
                    {
                        Probe(agentShape, Vector3.Lerp(previous, next, sample / (float)samples), 0.28f);
                        Require(BistroBuilderPhysicalPlacementGeometry.EvaluateConflict(bodyShapes, agentShape) ==
                            RestaurantPlacementConflictType.None, "Native route crossed a physical part of the U body.");
                    }
                    previous = next;
                }
                root.GetComponent<BistroBuilderAdaptiveSpatialProxy>().Parts[0].size = new Vector2(float.NaN, 4f);
                probe.ConfigureRuntime(Vector3.zero, new Vector2(0.4f, 0.4f));
                Require(validation.ValidatePlacement(bodyMember, Vector3.zero, Quaternion.identity).Status ==
                    RestaurantPlacementValidationStatus.SystemUnavailable, "Invalid compound candidate was accepted by native placement.");
                Require(validation.ValidatePlacement(member, Vector3.zero, Quaternion.identity).Status ==
                    RestaurantPlacementValidationStatus.PhysicalOverlap, "Invalid existing body stopped blocking placement conservatively.");
                probe.ConfigureRuntime(Vector3.zero, Vector2.one, 0f, false);
                navigation.RebuildNavigationTopology();
                Require(navigation.StaticObstacleCount == 1 && !navigation.TryBuildRoute("compound_waiter",
                    BistroBuilderNavigationAgentMask.Waiter, origin, Vector3.zero, 0.28f, points, out _, out _),
                    "Invalid compound data left its cavity traversable.");
                Debug.Log("[SAVIC] COMPOUND NATIVE PLACEMENT AND ROUTE - PASS: area boundaries, cavity, wall, invalid candidate/blocker; waiter route=" + kind + ", length=" + length.ToString("F3") + "m.");
            }
            finally
            {
                Object.DestroyImmediate(candidate); Object.DestroyImmediate(root);
                Object.DestroyImmediate(services); Object.DestroyImmediate(areaObject); Object.DestroyImmediate(definition);
            }
        }

        private static GameObject MakeBody(bool hidden)
        {
            GameObject root = new GameObject("Compound U body probe");
            if (hidden) root.hideFlags = HideFlags.HideAndDontSave;
            root.AddComponent<RestaurantPlacementFootprint>().ConfigureRuntime(Vector3.zero, new Vector2(4f, 4f));
            BistroBuilderAdaptiveSpatialProxy proxy = root.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
            proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Compound);
            proxy.AddPart(Part("left", new Vector3(-1.75f, 0f, 0f), new Vector2(0.5f, 4f)));
            proxy.AddPart(Part("right", new Vector3(1.75f, 0f, 0f), new Vector2(0.5f, 4f)));
            proxy.AddPart(Part("front", new Vector3(0f, 0f, 1.75f), new Vector2(3f, 0.5f)));
            root.AddComponent<BistroBuilderSpatialPhysicalFootprintAdapter>();
            return root;
        }
        private static BistroBuilderSpatialProxyPart Part(string id, Vector3 center, Vector2 size) =>
            new BistroBuilderSpatialProxyPart { partId = id, localCenter = center, size = size };
        private static bool Write(RestaurantPlacementFootprint footprint, List<RestaurantPlacementShape> shapes) =>
            BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(footprint, footprint.transform.position,
                footprint.transform.rotation, shapes, out _);
        private static void Probe(List<RestaurantPlacementShape> shapes, Vector3 center, float half, float clearance = 0f)
        {
            shapes.Clear();
            shapes.Add(new RestaurantPlacementShape(center, Vector3.right, Vector3.forward, Vector2.one * half, clearance));
        }
        private static void Set(Object target, string field, Object value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(Object target, string field, string value)
        {
            SerializedObject data = new SerializedObject(target);
            data.FindProperty(field).stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
