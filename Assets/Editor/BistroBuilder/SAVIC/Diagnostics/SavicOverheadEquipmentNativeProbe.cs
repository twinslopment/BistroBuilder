using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicOverheadEquipmentNativeProbe
    {
        public static void VerifyFromCommandLine()
        {
            Require(Application.isBatchMode, "Isolated native overhead probe is batch-only.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = SavicEditorContext.Instance;
            var originals = context.Manifests.GetAll().Where(m => SavicOverheadEquipmentAuthoringPlanner.IsVerifiedHood(m, context.Layout)).ToArray();
            Require(originals.Length > 0, "No verified real overhead source available.");
            var objects = new List<GameObject>();
            var item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            try
            {
                var services = Make("Passive native services", objects);
                var placeables = services.AddComponent<RestaurantPlaceableRegistry>();
                var spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
                var assessment = services.AddComponent<BistroBuilderSpatialPlacementAssessmentService>(); assessment.ConfigureForEditor(spatial);
                var navigation = services.AddComponent<BistroBuilderNavigationService>();
                var adapter = new SavicOverheadEquipmentFunctionAdapter();
                var profile = SavicOverheadEquipmentAuthoringPlanner.GetOrCreateProfile();
                foreach (var original in originals)
                {
                    string manifestPath = Path.Combine(context.Layout.ManifestsRoot, original.savicId + ".json");
                    string originalBytesHash = SavicHashService.ComputeSha256(manifestPath);
                    var m = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(original));
                    string archive = context.Layout.GetArchivedSourcePath(m.source.sourceHash, m.source.originalFileName);
                    string mirror = context.Layout.GetUnitySourceMirrorPath(m.source.sourceHash, m.source.originalFileName);
                    Require(SavicHashService.ComputeSha256(archive) == m.source.sourceHash && SavicHashService.ComputeSha256(mirror) == m.source.sourceHash,
                        "Archived and imported overhead originals must match their canonical SHA-256.");
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
                    Require(SavicOverheadEquipmentAuthoringPlanner.TryPlan(m, source, profile, out var plan, out string error, context.Layout), error);
                    Require(SavicOverheadEquipmentAuthoringPlanner.TryPlan(m, source, profile, out var repeated, out error, context.Layout) &&
                        plan.inputFingerprint == repeated.inputFingerprint, "Real overhead planning is not repeatable.");
                    m.overheadEquipment = plan;
                    var root = Make("Real overhead body " + original.savicId, objects);
                    var placeable = root.AddComponent<RestaurantPlaceableObject>(); placeable.SetItemDefinition(item);
                    Transform visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
                    var instance = Object.Instantiate(source, visual, false);
                    SavicGenericPlaceablePublisher.NormalizeSourceVisual(instance.transform, m.model3D);
                    adapter.Apply(root, m); adapter.Apply(root, m);
                    Require(adapter.Validate(root, m, out error), error);
                    Bounds bounds = root.GetComponentsInChildren<Renderer>(true)[0].bounds;
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(renderer.bounds);
                    Require(Vector3.Distance(bounds.size, plan.finalSizeMeters) < 0.001f &&
                        Math.Abs(bounds.min.y - plan.installationBottomMeters) < 0.001f,
                        "Actual imported renderers do not match uniformly normalized elevated geometry.");
                    var binding = root.GetComponent<BistroBuilderPassiveBodySpatialBinding>();
                    var subject = root.GetComponent<BistroBuilderSpatialSubject>();
                    var part = subject.Proxy.Parts[0];
                    placeable.AssignInstanceId("overhead_real_" + original.savicId);
                    spatial.RebuildSubjects(); navigation.RebuildNavigationTopology();
                    Require(!subject.IsRegistrationEligible && spatial.SubjectCount == 0 && navigation.StaticObstacleCount == 0 &&
                        !binding.TryCompleteActivation(out _), "Provisional overhead body escaped into native scene authorities.");
                    // A pre-existing intersecting claim must reject the registration transaction.
                    var overlapping = BistroBuilderSpatialVolume.Circle(new Vector3(0f, 2.4f, 0f), 0.28f)
                        .WithHeightRange(BistroBuilderSpatialHeightRange.Between(2.3f, 2.6f));
                    Require(spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "overhead.guard",
                        volume = overlapping, durationSeconds = 0f }, out var busy, out _), "Native fixture lease failed.");
                    Require(!placeables.RegisterPlaceable(placeable) && !placeables.ContainsPlaceable(placeable) && spatial.SubjectCount == 0,
                        "Active claim did not roll back passive registration.");
                    spatial.ReleaseLease(busy.leaseId);
                    Require(placeables.RegisterPlaceable(placeable) && binding.TryCompleteActivation(out error), error);
                    string id = BistroBuilderPassiveBodySpatialBinding.BuildBodyId(placeable.InstanceId);
                    Require(subject.SubjectId == id && spatial.SubjectCount == 1, "Passive identity or idempotence failed.");
                    spatial.RebuildSubjects(); navigation.RebuildNavigationTopology();
                    Require(spatial.SubjectCount == 1 && navigation.StaticObstacleCount == 1, "Real scene rebuild lost the passive body.");
                    var low = BistroBuilderSpatialVolume.Circle(Vector3.zero, 0.28f)
                        .WithHeightRange(BistroBuilderSpatialHeightRange.Between(0f, 2f));
                    Require(spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "overhead.lower.person", volume = low,
                        durationSeconds = 0f, validateAgainstStaticGeometry = true }, out var lower, out _), "Real hood blocked the certified lower human claim.");
                    Require(binding.CanDeactivate(out error), error);
                    spatial.ReleaseLease(lower.leaseId);
                    Require(!spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "overhead.unknown", volume = low.WithHeightRange(default),
                        validateAgainstStaticGeometry = true }, out _, out _), "Unknown claim height released the real body.");
                    var route = new List<Vector3>(); var origin = new Vector3(0f, 0f, -3f); var destination = new Vector3(0f, 0f, 3f);
                    Require(navigation.TryBuildRoute("overhead.real.walker", BistroBuilderNavigationAgentMask.Waiter, origin, destination,
                        0.28f, route, out float meters, out var kind) && kind != BistroBuilderNavigationRouteKind.DirectDegraded,
                        "Real normalized hood did not allow a native lower route.");
                    var physical = part.BuildWorldVolume(root.transform); Vector3 previous = origin; float closest = float.MaxValue;
                    foreach (var next in route)
                    {
                        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, next) / 0.05f));
                        for (int i = 0; i <= samples; i++)
                        {
                            Vector3 point = Vector3.Lerp(previous, next, i / (float)samples);
                            closest = Mathf.Min(closest, new Vector2(point.x, point.z).magnitude);
                            Require(!physical.Overlaps(BistroBuilderSpatialVolume.Circle(point, 0.28f).WithHeightRange(
                                BistroBuilderSpatialHeightRange.Between(point.y, point.y + 2f))), "Human envelope crossed real hood body.");
                        }
                        previous = next;
                    }
                    Require(closest < 0.25f, "Route did not demonstrate passage below the real source.");
                    Require(spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "overhead.busy.removal", volume = overlapping,
                        durationSeconds = 0f }, out busy, out _) && !binding.CanDeactivate(out _), "Busy removal guard did not query active leases.");
                    spatial.ReleaseLease(busy.leaseId);
                    float height = part.height; part.height = float.NaN;
                    Require(!adapter.Validate(root, m, out _) && !binding.CanActivate(out _), "Invalid height was accepted by native authoring.");
                    part.height = height;
                    placeables.UnregisterPlaceable(placeable); spatial.RebuildSubjects(); navigation.RebuildNavigationTopology();
                    Require(spatial.SubjectCount == 0 && navigation.StaticObstacleCount == 0 && string.IsNullOrEmpty(subject.SubjectId),
                        "Passive removal/rebuild leaked geometry or identity.");
                    Require(placeables.RegisterPlaceable(placeable), "Passive body could not reactivate with the same identity.");
                    placeables.UnregisterPlaceable(placeable);
                    Require(SavicHashService.ComputeSha256(manifestPath) == originalBytesHash && spatial.SubjectCount == 0 && spatial.ActiveLeaseCount == 0,
                        "Read-only probe altered source state or leaked native resources.");
                    Debug.Log("[SAVIC] REAL OVERHEAD AUTHORING/NATIVE BODY - PASS " + original.savicId + "; size=" + plan.finalSizeMeters.ToString("F4") +
                        "; bottom=" + plan.installationBottomMeters + "; route=" + kind + " " + meters.ToString("F3") +
                        "m sampled5cm; sourceSHA, uniform geometry, provisional, transaction, claims, repeat activation and cleanup. Catalog/PlayMode SaveGame not claimed.");
                    Object.DestroyImmediate(root); objects.Remove(root);
                }
            }
            finally { for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]); Object.DestroyImmediate(item); }
        }
        private static GameObject Make(string name, List<GameObject> objects) { var result = new GameObject(name); objects.Add(result); return result; }
        private static void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException(error); }
    }
}
