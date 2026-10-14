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
    [InitializeOnLoad]
    public static class SavicOverheadEquipmentRuntimePlaytest
    {
        private const string Key = "SAVIC.OverheadRuntime.";
        private static SavicManifest expected;
        private static RestaurantPlaceableItemDefinition item;
        private static RestaurantPlaceableRegistry registry;
        private static RestaurantPlaceableCreationService creation;
        private static RestaurantPlaceableLifecycleService lifecycle;
        private static RestaurantEditModeService editMode;
        private static BistroBuilderSaveGameService save;
        private static BistroBuilderSpatialInteractionService spatial;
        private static BistroBuilderNavigationService navigation;
        private static RestaurantPlaceableCatalogDefinition candidateCatalog;
        private static string instanceId;
        private static int previousUnityId, loads, slot;
        private static double deadline;
        private static bool candidateMode;

        static SavicOverheadEquipmentRuntimePlaytest()
        { EditorApplication.playModeStateChanged += Changed; EditorApplication.update += Tick; Application.logMessageReceived += Log; }
        public static void RunCandidateFromCommandLine() => Begin(true);
        public static void RunPublishedFromCommandLine() => Begin(false);
        public static void RevalidatePublishedCandidateFromCommandLine() => Begin(true, true);
        public static void PublishVerifiedFromCommandLine()
        {
            var context = SavicEditorContext.Instance;
            context.CanonicalReconciliation.RetryVerifiedOverheadReviews(4);
            for (int i = 0; i < 48 && context.Batch.TickOneIgnoringCooldownForDiagnostics(); i++) { }
            var matches = context.Manifests.GetAll().Where(m => SavicOverheadEquipmentRuntimeAcceptance.Required(m)).ToArray();
            Require(matches.Length > 0 && matches.All(m => m.status == "PUBLISHED" && SavicOverheadEquipmentRuntimeAcceptance.Matches(m, context.Layout)),
                "Canonical queue did not publish the current accepted overhead candidate.");
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
            Debug.Log("[SAVIC] OVERHEAD CANONICAL PUBLICATION - PASS: verified runtime acceptance consumed through the existing queue/family/transaction.");
        }
        internal static void RunSelected(string id, bool candidate) => Begin(candidate, false, id);
        private static void Begin(bool candidate, bool revalidatingPublished = false, string selectedId = null)
        {
            Require((Application.isBatchMode || SavicRuntimeVerificationSession.IsActive) && !EditorApplication.isPlayingOrWillChangePlaymode, "Isolated overhead Play Mode acceptance requires an idle batch editor.");
            var context = SavicEditorContext.Instance;
            if (candidate && !revalidatingPublished && selectedId == null)
            {
                context.CanonicalReconciliation.RetryVerifiedOverheadReviews(4);
                for (int i = 0; i < 48 && context.Batch.TickOneIgnoringCooldownForDiagnostics(); i++) { }
            }
            var manifests = context.Manifests.GetAll().Where(m => SavicOverheadEquipmentRuntimeAcceptance.Required(m) &&
                m.status == (candidate && !revalidatingPublished ? "NEEDS_REVIEW" : "PUBLISHED") && (selectedId == null || m.savicId == selectedId)).ToArray();
            Require(manifests.Length == 1, "This bounded acceptance run needs one canonical overhead candidate.");
            var m = manifests[0];
            string archive = context.Layout.GetArchivedSourcePath(m.source.sourceHash, m.source.originalFileName);
            string mirror = context.Layout.GetUnitySourceMirrorPath(m.source.sourceHash, m.source.originalFileName);
            Require(SavicHashService.ComputeSha256(archive) == m.source.sourceHash && SavicHashService.ComputeSha256(mirror) == m.source.sourceHash,
                "Original overhead source SHA-256 changed.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
            Require(SavicOverheadEquipmentAuthoringPlanner.TryPlan(m, source, SavicOverheadEquipmentAuthoringPlanner.GetOrCreateProfile(),
                out var physical, out string error, context.Layout) && physical.inputFingerprint == m.overheadEquipment.inputFingerprint, error);
            Require(new SavicOverheadEquipmentFunctionAdapter().Validate(AssetDatabase.LoadAssetAtPath<GameObject>(m.genericPlaceable.prefabAssetPath), m, out error), error);
            if (revalidatingPublished)
            {
                var previous = m.overheadEquipmentRuntime;
                Require(previous != null && previous.verifierVersion == SavicOverheadEquipmentRuntimeAcceptance.Version &&
                    previous.sourceHash == m.source.sourceHash && previous.planFingerprint == m.overheadEquipment.inputFingerprint &&
                    previous.creationPassed && previous.provisionalIsolationPassed && previous.nativeBindingPassed && previous.areaCapabilityPassed &&
                    previous.routePassed && previous.leasePassed && previous.saveLoadPassed && previous.repeatedLoadPassed && previous.cleanupPassed && previous.consoleClean &&
                    previous.reportRelativePath == context.Layout.ToProjectRelativePath(Path.Combine(context.Layout.LogsRoot,
                        "savic-overhead-candidate-" + m.savicId + "-runtime-playtest.txt")) &&
                    File.Exists(context.Layout.FromProjectRelativePath(previous.reportRelativePath)) &&
                    SavicHashService.ComputeSha256(context.Layout.FromProjectRelativePath(previous.reportRelativePath)) == previous.reportHash,
                    "Integration revalidation requires intact prior source/plan/report and completed acceptance; runtime is repeated without changing publication state.");
            }
            if (!candidate) Require(SavicOverheadEquipmentRuntimeAcceptance.Matches(m, context.Layout), "Main-catalog acceptance requires current previous runtime proof.");
            SessionState.SetString(Key + "Expected", JsonUtility.ToJson(m)); SessionState.SetBool(Key + "Candidate", candidate);
            SessionState.SetBool(Key + "Success", false); SessionState.SetInt(Key + "Errors", 0);
            SessionState.SetString(Key + "Result", "Not completed."); SessionState.SetString(Key + "Stage", "enter");
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (string.IsNullOrEmpty(SessionState.GetString(Key + "Stage", ""))) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                expected = JsonUtility.FromJson<SavicManifest>(SessionState.GetString(Key + "Expected", ""));
                candidateMode = SessionState.GetBool(Key + "Candidate", false); loads = 0; slot = -1;
                deadline = EditorApplication.timeSinceStartup + 120; SessionState.SetString(Key + "Stage", "init");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                bool success = SessionState.GetBool(Key + "Success", false) && SessionState.GetInt(Key + "Errors", 0) == 0;
                var context = SavicEditorContext.Instance;
                var original = JsonUtility.FromJson<SavicManifest>(SessionState.GetString(Key + "Expected", ""));
                string report = Path.Combine(context.Layout.LogsRoot, "savic-overhead-candidate-" + original.savicId + "-runtime-playtest.txt");
                try
                {
                    string message = SessionState.GetString(Key + "Result", "Unknown failure.");
                    if (SessionState.GetInt(Key + "Errors", 0) > 0) message += " Console: " + SessionState.GetString(Key + "Error", "");
                    File.WriteAllText(report, (success ? "PASS: " : "FAIL: ") + message + "\n" + DateTime.UtcNow.ToString("O"));
                    if (success)
                    {
                        Require(context.Manifests.TryGetBySavicId(original.savicId, out var m) &&
                            m.overheadEquipment.inputFingerprint == original.overheadEquipment.inputFingerprint &&
                            m.source.sourceHash == original.source.sourceHash, "Overhead source/plan changed during runtime verification.");
                        Require(new SavicOverheadEquipmentFunctionAdapter().Validate(AssetDatabase.LoadAssetAtPath<GameObject>(m.genericPlaceable.prefabAssetPath), m, out _),
                            "Overhead prefab authoring changed during verification.");
                        m.overheadEquipmentRuntime = new SavicOverheadEquipmentRuntimeAcceptanceRecord {
                            verifierVersion = SavicOverheadEquipmentRuntimeAcceptance.Version, sourceHash = m.source.sourceHash,
                            planFingerprint = m.overheadEquipment.inputFingerprint,
                            prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(m.genericPlaceable.prefabAssetPath).ToString(),
                            reportRelativePath = context.Layout.ToProjectRelativePath(report), reportHash = SavicHashService.ComputeSha256(report),
                            verifiedUtc = DateTime.UtcNow.ToString("O"), catalogMode = SessionState.GetBool(Key + "Candidate", false) ? "candidate" : "main",
                            creationPassed = true, provisionalIsolationPassed = true, nativeBindingPassed = true, areaCapabilityPassed = true,
                            routePassed = true, leasePassed = true, saveLoadPassed = true, repeatedLoadPassed = true, cleanupPassed = true, consoleClean = true };
                        context.Manifests.Save(m);
                        Require(SavicOverheadEquipmentRuntimeAcceptance.Matches(m, context.Layout), "Completed overhead proof does not match its current artifacts.");
                    }
                }
                catch (Exception e) { success = false; File.WriteAllText(report, "FAIL: " + e.Message); }
                SessionState.EraseString(Key + "Stage");
                if (success) Debug.Log("[SAVIC] OVERHEAD RUNTIME ACCEPTANCE - " + File.ReadAllText(report));
                else Debug.LogError("[SAVIC] OVERHEAD RUNTIME ACCEPTANCE - " + File.ReadAllText(report));
                if (SavicRuntimeVerificationSession.IsActive) SavicRuntimeVerificationSession.Complete(success, File.ReadAllText(report));
                else if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            }
        }
        private static void Log(string message, string trace, LogType type)
        {
            if (string.IsNullOrEmpty(SessionState.GetString(Key + "Stage", "")) ||
                (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1); SessionState.SetString(Key + "Error", message);
        }
        private static void Tick()
        {
            string stage = SessionState.GetString(Key + "Stage", "");
            if (!EditorApplication.isPlaying || string.IsNullOrEmpty(stage) || stage == "exit") return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    if (stage == "cleanup" || stage == "delete")
                    { SessionState.SetString(Key + "Result", "Cleanup timed out in " + stage); Exit(false); }
                    else Finish(false, "Timeout in " + stage);
                    return;
                }
                if (stage == "init" && Time.frameCount >= 3) Initialize();
                else if (stage == "cleanup" && (save == null || !save.IsBusy)) DeleteSlot();
            }
            catch (Exception e)
            {
                if (SessionState.GetString(Key + "Stage", "") == "delete")
                { SessionState.SetString(Key + "Result", "Diagnostic cleanup failed: " + e.Message); Exit(false); }
                else Finish(false, e.Message);
            }
        }
        private static void Initialize()
        {
            registry = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>();
            creation = Object.FindFirstObjectByType<RestaurantPlaceableCreationService>();
            lifecycle = Object.FindFirstObjectByType<RestaurantPlaceableLifecycleService>();
            editMode = Object.FindFirstObjectByType<RestaurantEditModeService>(); save = Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
            spatial = Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>(); navigation = Object.FindFirstObjectByType<BistroBuilderNavigationService>();
            item = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(expected.genericPlaceable.itemDefinitionAssetPath);
            Require(registry != null && creation != null && lifecycle != null && editMode != null && save != null && spatial != null && navigation != null && item != null,
                "Native runtime authorities are missing.");
            if (registry.RegisteredPlaceableCount == 0) return;
            for (int i = 960; i <= 979; i++) if (!save.SlotExists(i)) { slot = i; break; }
            Require(slot >= 0, "No free diagnostic save slot.");
            ConfigureCatalog();
            Require(editMode.IsEditModeActive || editMode.TryEnterEditMode(out _, out _), "Cannot enter canonical Edit Mode.");
            var floorReference = registry.RegisteredPlaceables.FirstOrDefault(p => p != null && p.GetComponent<RestaurantTable>() != null);
            Require(floorReference != null, "No canonical placement anchor supplies the existing scene floor height.");
            float floorY = floorReference.PlacementAnchor.position.y;
            string last = "No food-production area."; int attempts = 0;
            foreach (var area in Object.FindObjectsByType<RestaurantArea>(FindObjectsSortMode.None)
                .Where(a => a.Definition != null && a.Definition.Capabilities.Any(c => c != null && c.CapabilityId == "food_production")))
                foreach (var boundary in area.BoundaryColliders)
                {
                    if (boundary == null || !boundary.enabled) continue;
                    Bounds bounds = boundary.bounds;
                    for (float x = bounds.min.x + 1.1f; x <= bounds.max.x - 1.1f; x += 0.65f)
                        for (float z = bounds.min.z + 0.6f; z <= bounds.max.z - 0.6f; z += 0.65f)
                        {
                            if (++attempts > 120) throw new InvalidOperationException("No canonical kitchen pose/underpass accepted after bounded attempts: " + last);
                            Vector3 anchor = new Vector3(x, floorY, z);
                            if (!area.ContainsPosition(anchor) || !creation.TryBeginCreation(item, anchor, Quaternion.identity, null, out var provisional, out var begin)) continue;
                            try
                            {
                                var candidateSubject = provisional.GetComponent<BistroBuilderSpatialSubject>();
                                Require(candidateSubject != null && !candidateSubject.IsRegistrationEligible &&
                                    !registry.ContainsPlaceable(provisional) && !spatial.RegisterSubject(candidateSubject), "Provisional body registered before commit.");
                                VerifyRoute(provisional);
                                if (!creation.TryCommitActiveCreation(out var commit)) { last = commit.Message; continue; }
                                instanceId = provisional.InstanceId; previousUnityId = provisional.GetInstanceID();
                                VerifyRuntime(provisional);
                                Require(editMode.TryExitEditMode(true, out _), "Cannot close Edit Mode.");
                                save.OperationCompleted += Saved;
                                SessionState.SetString(Key + "Stage", "save"); deadline = EditorApplication.timeSinceStartup + 120;
                                Require(save.TrySaveSlot(slot, "SAVIC PASSIVE OVERHEAD ACCEPTANCE", out string error), error); return;
                            }
                            catch (Exception e) { last = e.Message; if (registry.ContainsPlaceable(provisional)) throw; }
                            finally { if (creation.HasActiveCreation) creation.TryCancelActiveCreation(out _); }
                        }
                }
            throw new InvalidOperationException("No canonical kitchen pose/underpass accepted: " + last);
        }
        private static void ConfigureCatalog()
        {
            var catalog = Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>();
            var definitions = Object.FindFirstObjectByType<BistroBuilderSaveDefinitionCatalog>();
            Require(catalog?.CatalogDefinition != null && definitions != null, "Native catalog/save resolvers are missing.");
            if (candidateMode)
            {
                candidateCatalog = Object.Instantiate(catalog.CatalogDefinition);
                var data = new SerializedObject(candidateCatalog); var items = data.FindProperty("items");
                items.arraySize++; items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item; data.ApplyModifiedPropertiesWithoutUndo();
                var service = new SerializedObject(catalog); service.FindProperty("catalogDefinition").objectReferenceValue = candidateCatalog; service.ApplyModifiedPropertiesWithoutUndo(); catalog.RebuildCatalog();
                var saved = new SerializedObject(definitions); var sources = saved.FindProperty("sourceCatalogs");
                sources.arraySize++; sources.GetArrayElementAtIndex(sources.arraySize - 1).objectReferenceValue = candidateCatalog; saved.ApplyModifiedPropertiesWithoutUndo(); definitions.RebuildIndex();
            }
            Require(catalog.TryGetItem(item.ItemId, out var resolved) && resolved == item && definitions.TryGetDefinition(item.ItemId, out var savedItem) && savedItem == item,
                "Runtime catalog/save cannot resolve the exact authored overhead item.");
        }
        private static void VerifyRoute(RestaurantPlaceableObject target)
        {
            navigation.RebuildNavigationTopology();
            Vector3 center = target.PlacementAnchor.position;
            Vector3 direction = target.transform.forward;
            Vector3 origin = center - direction * 1.1f, destination = center + direction * 1.1f;
            var route = new List<Vector3>();
            Require(navigation.TryBuildRoute("savic.overhead.walker", BistroBuilderNavigationAgentMask.Waiter, origin, destination, 0.28f,
                route, out _, out var kind) && kind != BistroBuilderNavigationRouteKind.DirectDegraded, "Native route below the hood is not available at this pose.");
            var physical = target.GetComponent<BistroBuilderSpatialSubject>().Proxy.Parts[0].BuildWorldVolume(target.transform);
            float closest = float.MaxValue; Vector3 previous = origin;
            foreach (Vector3 next in route)
            {
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, next) / 0.05f));
                for (int n = 0; n <= samples; n++)
                {
                    Vector3 point = Vector3.Lerp(previous, next, n / (float)samples);
                    closest = Mathf.Min(closest, new Vector2(point.x - center.x, point.z - center.z).magnitude);
                    Require(!physical.Overlaps(BistroBuilderSpatialVolume.Circle(point, 0.28f).WithHeightRange(
                        BistroBuilderSpatialHeightRange.Between(point.y, point.y + 2f))), "Runtime route penetrated the actual hood body.");
                }
                previous = next;
            }
            Require(closest < 0.25f, "Route avoided the hood and did not prove passage below it.");
        }
        private static void VerifyRuntime(RestaurantPlaceableObject target)
        {
            Require(new SavicOverheadEquipmentFunctionAdapter().Validate(target.gameObject, expected, out string error), error);
            var member = target.GetComponent<RestaurantAreaMember>();
            Require(member?.AssignedArea?.Definition != null && member.AssignedArea.Definition.Capabilities.Any(c => c != null && c.CapabilityId == "food_production"),
                "Overhead equipment is not assigned to a real food-production area.");
            string id = BistroBuilderPassiveBodySpatialBinding.BuildBodyId(target.InstanceId);
            Require(spatial.TryGetSubject(id, out var subject) && subject.gameObject == target.gameObject && subject.IsRegistrationEligible,
                "Runtime passive identity was not derived from the persisted placeable instance.");
            Require(SavicSourceMaterialFallback.CountInvalidSlots(target.gameObject) == 0, "Runtime hood has invalid materials.");
            VerifyRoute(target);
            Vector3 center = target.PlacementAnchor.position;
            var low = BistroBuilderSpatialVolume.Circle(center, 0.28f).WithHeightRange(BistroBuilderSpatialHeightRange.Between(center.y, center.y + 2f));
            Require(spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "savic.overhead.lower", volume = low,
                durationSeconds = 0f, validateAgainstStaticGeometry = true }, out var lease, out _), "Runtime lower human lease is blocked.");
            try { Require(target.GetComponent<BistroBuilderPassiveBodySpatialBinding>().CanDeactivate(out error), error); }
            finally { spatial.ReleaseLease(lease.leaseId); }
            Require(!spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "savic.overhead.head", volume = low.WithHeightRange(
                BistroBuilderSpatialHeightRange.Between(center.y, center.y + 2.4f)), validateAgainstStaticGeometry = true }, out _, out _) &&
                !spatial.TryAcquireLease(new BistroBuilderSpatialClaimRequest { ownerId = "savic.overhead.unknown", volume = low.WithHeightRange(default),
                    validateAgainstStaticGeometry = true }, out _, out _), "Head penetration or unknown claim height released the runtime body.");
            Debug.Log("[SAVIC] OVERHEAD NATIVE RUNTIME - PASS: food_production, registered bounded body, native underpass sampled5cm, lower lease, head/unknown negatives; load=" + loads);
        }
        private static void Saved(BistroBuilderSaveOperationResult result)
        {
            try
            {
                string stage = SessionState.GetString(Key + "Stage", "");
                Require(result != null && result.Succeeded, result?.Message ?? "SaveGame returned no result.");
                if (stage == "delete") { Require(!save.SlotExists(slot), "Diagnostic slot remains on disk."); Exit(true); return; }
                if (stage == "save") { SessionState.SetString(Key + "Stage", "load"); Require(save.TryLoadSlot(slot, out string error), error); return; }
                if (stage != "load") return;
                var restored = registry.RegisteredPlaceables.SingleOrDefault(p => p != null && p.InstanceId == instanceId);
                Require(restored != null && restored.GetInstanceID() != previousUnityId && restored.ItemDefinition == item,
                    "SaveGame did not recreate the same overhead ItemId/InstanceId with a fresh Unity object.");
                loads++; VerifyRuntime(restored);
                if (loads < 2) { previousUnityId = restored.GetInstanceID(); deadline = EditorApplication.timeSinceStartup + 120; Require(save.TryLoadSlot(slot, out string error), error); return; }
                Require(editMode.IsEditModeActive || editMode.TryEnterEditMode(out _, out _), "Cannot enter Edit Mode for native cleanup.");
                Require(lifecycle.TryDeactivateInstance(restored, out _, out var removal), removal.Message);
                spatial.RebuildSubjects(); navigation.RebuildNavigationTopology();
                Require(!registry.TryGetByInstanceId(instanceId, out _) && !spatial.TryGetSubject(BistroBuilderPassiveBodySpatialBinding.BuildBodyId(instanceId), out _),
                    "Canonical cleanup left a passive instance or spatial body.");
                Require(editMode.TryExitEditMode(true, out _), "Cannot close cleanup Edit Mode.");
                Finish(true, (candidateMode ? "Candidate catalog" : "Actual MainCatalog/SaveDefinitionCatalog") +
                    "; originalSHA/currentPlan, provisional isolation, real kitchen placement, native bounded body/claims/underpass, two real SaveGame loads with stable ItemId/InstanceId and fresh objects, canonical cleanup, diagnostic slot deleted, Console clean through Editor. No extraction/ventilation gameplay or ceiling inferred.");
            }
            catch (Exception e)
            {
                if (SessionState.GetString(Key + "Stage", "") == "delete")
                { SessionState.SetString(Key + "Result", "Diagnostic cleanup failed: " + e.Message); Exit(false); }
                else Finish(false, e.Message);
            }
        }
        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key + "Success", success); SessionState.SetString(Key + "Result", message);
            if (save != null) save.OperationCompleted -= Saved;
            SessionState.SetString(Key + "Stage", "cleanup"); deadline = EditorApplication.timeSinceStartup + 30;
        }
        private static void DeleteSlot()
        {
            if (slot >= 0 && save != null && save.SlotExists(slot))
            {
                save.OperationCompleted -= Saved; save.OperationCompleted += Saved; SessionState.SetString(Key + "Stage", "delete");
                if (!save.TryDeleteSlot(slot, out string error)) { SessionState.SetString(Key + "Result", "Diagnostic delete rejected: " + error); Exit(false); }
            }
            else Exit(SessionState.GetBool(Key + "Success", false));
        }
        private static void Exit(bool success)
        {
            // A successful delete must not convert an earlier test failure to success.
            SessionState.SetBool(Key + "Success", success && SessionState.GetBool(Key + "Success", false));
            if (save != null) save.OperationCompleted -= Saved;
            SessionState.SetString(Key + "Stage", "exit"); EditorApplication.ExitPlaymode();
        }
        private static void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException(error); }
    }
}
