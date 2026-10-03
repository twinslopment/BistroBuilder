using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBarCounterAuthoringSelfTest
    {
        public static void RunFromCommandLine()
        {
            VerifyAcceptanceQueueGuards();
            List<GameObject> roots = new List<GameObject>();
            SavicBarCounterProfile profile = ScriptableObject.CreateInstance<SavicBarCounterProfile>();
            try
            {
                GameObject source = MakeU(0.5f); roots.Add(source);
                AddBox(source, new Vector3(0.1f, 0.4f, 0.1f), new Vector3(0, 1.2f, 1.75f));
                SavicManifest manifest = MakeManifest(source);
                Require(SavicBarCounterAuthoringPlanner.TryPlan(manifest, source, profile, out var plan, out string error), error);
                Require(Math.Abs(plan.sourceCounterHeightMeters - 1f) < 0.0001f &&
                    Math.Abs(plan.uniformScale - profile.counterHeightMeters) < 0.0001f &&
                    plan.waiterLocalPosition.y == 0 && plan.openingDirection == Vector3.back &&
                    plan.customerLocalPosition.z > plan.finalSizeMeters.z * 0.5f && plan.physicalBoxes.Count > 2,
                    "Countertop/apex, normalization or source-derived service points are incorrect.");
                Require(SavicBarCounterAuthoringPlanner.TryPlan(manifest, source, profile, out var repeated, out error) &&
                    repeated.inputFingerprint == plan.inputFingerprint, "Canonical authoring plan is not repeatable: " + error);
                string originalFingerprint = plan.inputFingerprint;
                plan.physicalBoxes[0].size.x += 0.01f;
                Require(SavicBarCounterAuthoringPlanner.ComputeFingerprint(plan) != originalFingerprint,
                    "Physical geometry changes did not invalidate the plan fingerprint.");
                profile.counterHeightMeters = 1.1f;
                Require(SavicBarCounterAuthoringPlanner.TryPlan(manifest, source, profile, out var taller, out error) &&
                    taller.inputFingerprint != originalFingerprint && taller.profileFingerprint != repeated.profileFingerprint,
                    "Physical profile changes did not invalidate authoring: " + error);
                profile.counterHeightMeters = 1.05f;
                manifest.barCounter = repeated;
                manifest.genericPlaceable.requiresFunctionalAdapter = true;
                manifest.genericPlaceable.integrationMode = SavicBarCounterFunctionAdapter.Mode;
                VerifyFunctionAuthoring(source, manifest);
                profile.customerOffsetMeters = float.NaN;
                Require(!SavicBarCounterAuthoringPlanner.TryPlan(manifest, source, profile, out _, out _), "Invalid profile was accepted.");
                profile.customerOffsetMeters = 0.45f;
                manifest.source.originalFileName = "bar_stool.glb";
                Require(!SavicBarCounterAuthoringPlanner.TryPlan(manifest, source, profile, out _, out _), "Bar stool was classified as a counter.");
                manifest.source.originalFileName = "curved_bar.glb";
                manifest.model3D.heightMeters += 0.02f;
                Require(!SavicBarCounterAuthoringPlanner.TryPlan(manifest, source, profile, out _, out _), "Stale manifest geometry was accepted.");
                manifest.model3D = SavicModelAnalyzer.Analyze(source, SavicModelAnalysisMode.GenericStatic);
                GameObject tiered = MakeU(0.5f); roots.Add(tiered);
                AddBox(tiered, new Vector3(0.2f, 0.04f, 4f), new Vector3(-1.9f, 1.13f, 0));
                AddBox(tiered, new Vector3(0.2f, 0.04f, 4f), new Vector3(1.9f, 1.13f, 0));
                AddBox(tiered, new Vector3(3.6f, 0.04f, 0.2f), new Vector3(0, 1.13f, 1.9f));
                Require(SavicBarCounterAuthoringPlanner.TryPlan(MakeManifest(tiered), tiered, profile, out var upper, out error) &&
                    Math.Abs(upper.sourceCounterHeightMeters - 1.15f) < 0.0001f,
                    "Tiered counter used the broad lower worktop as the customer ledge: " + error);
                GameObject solid = new GameObject("Solid bar"); roots.Add(solid);
                AddBox(solid, new Vector3(4, 1, 4), new Vector3(0, 0.5f, 0));
                Require(!SavicBarCounterAuthoringPlanner.TryPlan(MakeManifest(solid), solid, profile, out _, out _),
                    "Solid named bar invented an accessible service interior.");
                GameObject narrow = MakeU(1.9f); roots.Add(narrow);
                Require(!SavicBarCounterAuthoringPlanner.TryPlan(MakeManifest(narrow), narrow, profile, out _, out _),
                    "Insufficient waiter clearance was accepted.");
                GameObject imported = Object.Instantiate(source); roots.Add(imported);
                foreach (Transform child in imported.transform) { child.localPosition *= 100; child.localScale *= 100; }
                imported.transform.localScale = Vector3.one * 0.01f;
                imported.transform.localRotation = Quaternion.Euler(0, 90, 0);
                imported.transform.position = new Vector3(11, -8, 21);
                Require(SavicBarCounterAuthoringPlanner.TryPlan(MakeManifest(imported), imported, profile, out var metric, out error) &&
                    Math.Abs(metric.uniformScale - repeated.uniformScale) < 0.0001f && metric.openingDirection == Vector3.left,
                    "Importer units/rotation or external root translation changed authoring: " + error);
                Debug.Log("[SAVIC] BAR COUNTER AUTHORING SELF-TEST - PASS: upper customer ledge/lower worktop/apex, physical profile, open service body, " +
                    "ports, repeatability/fingerprints, stale inputs, stool/solid/narrow/profile negatives and metric units/rotation. Publication not granted.");
            }
            finally
            {
                foreach (GameObject root in roots) if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
            }
        }

        public static void VerifyRealPlanFromCommandLine()
        {
            Require(Application.isBatchMode, "Real bar authoring probe requires an isolated batch scene.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RunFromCommandLine();
            SavicEditorContext context = SavicEditorContext.Instance;
            Report report = new Report { generatedUtc = DateTime.UtcNow.ToString("O") };
            foreach (SavicManifest original in context.Manifests.GetAll().Where(m => m?.status == "NEEDS_REVIEW"))
            {
                string subject = SavicProviderMetadataService.ResolveSemanticName(original, context.Layout);
                if (!Regex.IsMatch(subject.Replace('_', ' '), @"\bbar\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(subject.Replace('_', ' '), @"\b(stool|chair)\b", RegexOptions.IgnoreCase)) continue;
                string archive = context.Layout.GetArchivedSourcePath(original.source.sourceHash, original.source.originalFileName);
                string mirror = context.Layout.GetUnitySourceMirrorPath(original.source.sourceHash, original.source.originalFileName);
                Require(context.Layout.ToProjectRelativePath(archive) == original.source.archivedRelativePath &&
                    HasHash(archive, original.source.sourceHash) && HasHash(mirror, original.source.sourceHash) &&
                    SavicProviderMetadataService.TryReadVerified(context.Layout, original.source, out _),
                    "Bar source/archive/mirror/provider identity failed verification.");
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
                // Probe classification is isolated; this diagnostic never edits manifests or queue states.
                SavicManifest probe = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(original));
                probe.classification.type = "BarCounter"; probe.classification.score = 0.95f;
                Require(SavicBarCounterAuthoringPlanner.TryPlan(probe, source,
                    SavicBarCounterAuthoringPlanner.GetOrCreateProfile(), out var plan, out string error, context.Layout), error);
                report.entries.Add(new Entry { savicId = original.savicId, subject = subject, plan = plan });
                probe.barCounter = plan;
                probe.genericPlaceable.requiresFunctionalAdapter = true;
                probe.genericPlaceable.integrationMode = SavicBarCounterFunctionAdapter.Mode;
                VerifyFunctionAuthoring(source, probe, true);
                Debug.Log("[SAVIC] Verified real bar physical plan: " + original.savicId + ", source countertop=" +
                    plan.sourceCounterHeightMeters.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) +
                    "m, profile countertop=" + plan.counterHeightMeters + "m, scale=" + plan.uniformScale +
                    ", physical size=" + plan.finalSizeMeters + ", waiter clearance=" + plan.waiterClearanceMeters +
                    "m, boxes=" + plan.physicalBoxes.Count + ". Source lifecycle preserved.");
            }
            Require(report.entries.Count > 0, "No verified bar review was available for physical planning.");
            File.WriteAllText(Path.Combine(context.Layout.LogsRoot, "bar-counter-real-authoring-plan.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[SAVIC] REAL BAR COUNTER AUTHORING PLAN - PASS: " + report.entries.Count +
                " original/provider verified, plan recomputed with profile and fingerprints. Runtime publication remains pending.");
        }

        public static void VerifyClosureAndInventoryFromCommandLine()
        {
            SavicV1ClosureGate.RunFromMenu();
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
            Debug.Log("[SAVIC] BAR BODY AND PHYSICAL PLAN CLOSURE - PASS: gate and fresh inventory completed.");
        }
        public static void PrepareRealCandidateFromCommandLine()
        {
            RunFromCommandLine();
            SavicAutonomousClassificationAudit.ReconcileAndAuditFromCommandLine();
            SavicEditorContext context = SavicEditorContext.Instance;
            int candidates = 0;
            foreach (SavicManifest m in context.Manifests.GetAll().Where(m => m?.type == "BarCounter"))
            {
                Require(m.status == "NEEDS_REVIEW" && m.barCounter.planned && m.genericPlaceable.planned &&
                    AssetDatabase.LoadAssetAtPath<GameObject>(m.genericPlaceable.prefabAssetPath) != null &&
                    !m.genericPlaceableReadiness.validated && !m.genericPlaceableReadiness.catalogResolvable,
                    "Bar candidate was published early or was not prepared by the canonical queue.");
                candidates++;
            }
            Require(candidates > 0, "Canonical queue did not prepare a real bar counter candidate.");
            Debug.Log("[SAVIC] BAR COUNTER CANONICAL CANDIDATE - PASS: " + candidates + " prepared without main catalog entry or PUBLISHED status.");
        }

        public static void PrepareSurfaceUpgradeFromCommandLine()
        {
            Require(Application.isBatchMode, "Canonical bar upgrade requires an isolated batch editor.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = SavicEditorContext.Instance;
            var pending = context.Manifests.GetAll().Where(m => m?.type == "BarCounter" &&
                (m.status == "PUBLISHED" || m.status == "NEEDS_REVIEW")).ToArray();
            Require(pending.Length > 0, "No canonical bar is available for its surface upgrade.");
            int queued = context.CanonicalReconciliation.RefreshVerifiedPublishedBarAuthoring(16);
            Require(queued > 0, "No stale bar authoring was queued by canonical reconciliation.");
            int ticks = 0;
            while (ticks++ < 128 && context.Batch.TickOneIgnoringCooldownForDiagnostics()) { }
            foreach (var original in pending)
            {
                Require(context.Manifests.TryGetBySavicId(original.savicId, out var manifest) &&
                    manifest.status == "NEEDS_REVIEW" && manifest.genericPlaceable.planned &&
                    new SavicBarCounterFunctionAdapter().Validate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        manifest.genericPlaceable.prefabAssetPath), manifest, out _),
                    "Canonical bar upgrade did not preserve the real runtime acceptance requirement.");
            }
            Require(context.CanonicalReconciliation.RefreshVerifiedPublishedBarAuthoring(16) == 0,
                "The canonical bar authoring update was repeatedly queued.");
            Debug.Log("[SAVIC] CANONICAL BAR SURFACE UPGRADE - candidate prepared by its family/publisher transaction; real runtime acceptance pending.");
        }

        public static void FinalizeAcceptedRealCandidatesFromCommandLine()
        {
            Require(Application.isBatchMode, "Acceptance finalization probe requires an isolated batch editor.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var context = SavicEditorContext.Instance;
            var accepted = context.Manifests.GetAll().Where(m => m?.type == "BarCounter" &&
                SavicBarCounterRuntimeAcceptance.Matches(m, context.Layout)).ToArray();
            Require(accepted.Length > 0, "No current real runtime acceptance is available. Publication remains blocked.");
            foreach (var original in accepted)
            {
                var probe = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(original));
                probe.barCounterRuntime.consoleClean = false;
                Require(!SavicBarCounterRuntimeAcceptance.Matches(probe, context.Layout), "Runtime Console errors were accepted.");
                probe.barCounterRuntime.consoleClean = true;
                probe.barCounterRuntime.cleanupPassed = false;
                Require(!SavicBarCounterRuntimeAcceptance.Matches(probe, context.Layout), "Incomplete cleanup was accepted.");
                probe.barCounterRuntime.cleanupPassed = true;
                probe.barCounterRuntime.prefabDependencyHash = "stale";
                Require(!SavicBarCounterRuntimeAcceptance.Matches(probe, context.Layout), "Stale prefab proof was accepted.");
                probe.barCounterRuntime.prefabDependencyHash = original.barCounterRuntime.prefabDependencyHash;
                probe.barCounterRuntime.reportHash = "stale";
                Require(!SavicBarCounterRuntimeAcceptance.Matches(probe, context.Layout), "Changed runtime report was accepted.");
                probe.barCounterRuntime.reportHash = original.barCounterRuntime.reportHash;
                probe.barCounterRuntime.planFingerprint = "stale";
                Require(!SavicBarCounterRuntimeAcceptance.Matches(probe, context.Layout), "Stale authoring plan was accepted.");
            }
            context.CanonicalReconciliation.RetryVerifiedBarRuntimeAcceptances(16);
            int ticks = 0;
            while (ticks++ < 128 && context.Batch.TickOneIgnoringCooldownForDiagnostics()) { }
            foreach (var original in accepted)
            {
                Require(context.Manifests.TryGetBySavicId(original.savicId, out var current) && current.status == "PUBLISHED" &&
                    current.genericPlaceableReadiness.validated && current.genericPlaceableReadiness.catalogResolvable &&
                    SavicBarCounterRuntimeAcceptance.Matches(current, context.Layout),
                    "Canonical publication did not preserve current runtime acceptance and catalog readiness.");
            }
            Require(context.CanonicalReconciliation.RetryVerifiedBarRuntimeAcceptances(16) == 0, "Accepted publication was repeatedly queued.");
            SavicBarBodySpatialSelfTest.VerifyNativeAndCanonicalFromCommandLine();
            SavicV1ClosureGate.RunFromMenu();
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
            Debug.Log("[SAVIC] REAL BAR COUNTER CANONICAL PUBLICATION - PASS: current runtime proof, stale/incomplete negatives, native queue, catalog and regressions.");
        }

        private static void VerifyAcceptanceQueueGuards()
        {
            string root = Path.Combine(Path.GetTempPath(), "SAVIC_BarAcceptance_" + Guid.NewGuid().ToString("N"));
            try
            {
                var layout = new SavicStorageLayout(root); layout.EnsureInfrastructure();
                var job = new SavicJobRecord { jobId = "bar", manifestSavicId = "asset", sourceHash = "hash", batchEligible = true,
                    state = SavicJobState.NeedsReview.ToString(), reasonCode = "BAR_COUNTER_RUNTIME_ACCEPTANCE_PENDING", primaryStage = "FAMILY_PUBLICATION" };
                SavicAtomicFile.WriteJson(layout.QueueSnapshotPath, new SavicQueueSnapshot { schemaVersion = 1, jobs = new List<SavicJobRecord> { job } });
                var store = new SavicJobStore(layout);
                Require(!store.RetryVerifiedBarRuntimeAcceptance("bar", "asset", "wrong", "proof") &&
                    !store.RetryVerifiedBarRuntimeAcceptance("bar", "asset", "hash", ""), "Unverified identity/proof was queued.");
                Require(store.RetryVerifiedBarRuntimeAcceptance("bar", "asset", "hash", "proof") &&
                    store.Jobs[0].state == SavicJobState.Ingested.ToString() &&
                    !store.RetryVerifiedBarRuntimeAcceptance("bar", "asset", "hash", "proof"), "Acceptance retry bypassed processing or repeated.");
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        private static void VerifyFunctionAuthoring(GameObject source, SavicManifest manifest, bool native = false)
        {
            GameObject authored = new GameObject("Canonical bar function authoring probe");
            RestaurantPlaceableItemDefinition item = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            try
            {
                authored.AddComponent<RestaurantPlaceableObject>().SetItemDefinition(item);
                Transform visual = new GameObject("Visual").transform; visual.SetParent(authored.transform, false);
                GameObject imported = Object.Instantiate(source, visual, false);
                SavicGenericPlaceablePublisher.NormalizeSourceVisual(imported.transform, manifest.model3D);
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                Require(SavicPlaceableFunctionAdapters.Validate(authored, manifest, out string error), error);
                Bounds bounds = authored.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds)
                    .Aggregate((a, b) => { a.Encapsulate(b); return a; });
                Require(Vector3.Distance(bounds.size, manifest.barCounter.finalSizeMeters) < 0.001f && Math.Abs(bounds.min.y) < 0.001f,
                    "Uniform visual normalization differs from the actual imported source or lost its floor anchor.");
                int count = authored.GetComponentsInChildren<Collider>(true).Length;
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                Require(count == manifest.barCounter.physicalBoxes.Count &&
                    authored.GetComponentsInChildren<Collider>(true).Length == count &&
                    SavicPlaceableFunctionAdapters.Validate(authored, manifest, out error), "Compound authoring is not idempotent: " + error);
                BoxCollider wrongRoot = authored.AddComponent<BoxCollider>();
                Require(!SavicPlaceableFunctionAdapters.Validate(authored, manifest, out _), "Full root collider closed the physical cavity without detection.");
                Object.DestroyImmediate(wrongRoot);
                BoxCollider changed = authored.GetComponentInChildren<BoxCollider>(); changed.center += Vector3.right * 0.01f;
                Require(!SavicPlaceableFunctionAdapters.Validate(authored, manifest, out _), "Changed physical collider diverged from BBSIS without detection.");
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                Transform physical = authored.transform.Find(SavicBarCounterFunctionAdapter.BodyName); physical.localPosition = Vector3.right * 0.01f;
                Require(!SavicPlaceableFunctionAdapters.Validate(authored, manifest, out _), "Shifted collider container was accepted.");
                physical.localPosition = Vector3.zero;
                BistroBuilderBarServiceSpot spot = authored.GetComponent<BistroBuilderBarPlaceableBinding>().Spots[0];
                Require(spot.CounterSurfacePoint != null &&
                    Mathf.Abs(spot.CounterSurfacePoint.position.y - manifest.barCounter.counterHeightMeters) < 0.0001f,
                    "Counter surface height did not come from the canonical physical plan.");
                spot.CounterSurfacePoint.localPosition += Vector3.up * 0.01f;
                Require(!SavicPlaceableFunctionAdapters.Validate(authored, manifest, out _), "Altered bar counter surface was accepted.");
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                spot.WaiterServicePoint.localPosition = manifest.barCounter.physicalBoxes[0].center;
                Require(!SavicPlaceableFunctionAdapters.Validate(authored, manifest, out _), "Operational point inside physical body was accepted.");
                SavicPlaceableFunctionAdapters.Apply(authored, manifest);
                Require(SavicPlaceableFunctionAdapters.Validate(authored, manifest, out error) &&
                    !authored.GetComponent<BistroBuilderSpatialSubject>().IsRegistrationEligible,
                    "Final bar proposal failed validation or became globally active: " + error);
                Debug.Log("[SAVIC] BAR FUNCTION AUTHORING - PASS: imported source dimensions/floor anchor, " + count +
                    " matching collider/BBSIS pieces, native spot/body configuration, idempotence, provisional isolation and altered-body/point negatives. " +
                    "Runtime route/service/save-load not yet claimed.");
                if (native) VerifyNativeBar(authored, manifest);
            }
            finally { Object.DestroyImmediate(authored); Object.DestroyImmediate(item); }
        }
        private static void VerifyNativeBar(GameObject root, SavicManifest manifest)
        {
            GameObject services = new GameObject("Verified source bar native authorities");
            GameObject customerObject = new GameObject("Verified source bar customer");
            BistroBuilderBarPlaceableBinding binding = root.GetComponent<BistroBuilderBarPlaceableBinding>();
            RestaurantPlaceableRegistry placeables = services.AddComponent<RestaurantPlaceableRegistry>();
            BistroBuilderBarServiceRegistry bars = services.AddComponent<BistroBuilderBarServiceRegistry>();
            BistroBuilderSpatialInteractionService spatial = services.AddComponent<BistroBuilderSpatialInteractionService>();
            BistroBuilderSpatialPlacementAssessmentService assessment = services.AddComponent<BistroBuilderSpatialPlacementAssessmentService>();
            assessment.ConfigureForEditor(spatial);
            RestaurantPlaceableObject placeable = root.GetComponent<RestaurantPlaceableObject>();
            try
            {
                BistroBuilderSpatialContractDefinition contract = AssetDatabase.LoadAssetAtPath<BistroBuilderSpatialContractDefinition>(SavicBarCounterFunctionAdapter.ContractPath);
                binding.ConfigureForEditor(binding.Spots.ToArray(), contract, placeables, bars, spatial, assessment);
                BistroBuilderBarBodySpatialAdapter body = root.GetComponent<BistroBuilderBarBodySpatialAdapter>();
                body.ConfigureForEditor(binding, contract, placeables, spatial, assessment);
                placeable.AssignInstanceId("savic.source.bar.native.probe");
                Require(body.CanActivate(out string error) && !body.IsSpatialLifecycleActive && spatial.SubjectCount == 0, error);
                Require(placeables.RegisterPlaceable(placeable) && binding.IsRuntimeRegistered &&
                    spatial.SubjectCount == 2 && bars.RegisteredSpotCount == 1, "Real bar did not bind its native root and service plaza.");
                CustomerGroup group = customerObject.AddComponent<CustomerGroup>();
                Require(group.Initialize(99112, 1, BistroBuilderServiceMode.WaitingAtBar) &&
                    bars.TryAllocateSpot(group, BistroBuilderServiceMode.WaitingAtBar, out BistroBuilderBarServiceSpot allocated) &&
                    allocated == binding.Spots[0], "Real bar native service allocation failed.");
                BistroBuilderBarSpatialAdapter adapter = binding.Spots[0].GetComponent<BistroBuilderBarSpatialAdapter>();
                Require(adapter.TryAcquireCustomerLease(group, out error) && !body.CanDeactivate(out _),
                    "Real bar customer lease or busy removal guard failed: " + error);
                BistroBuilderNavigationService navigation = services.AddComponent<BistroBuilderNavigationService>();
                navigation.RebuildNavigationTopology();
                Require(navigation.StaticObstacleCount == manifest.barCounter.physicalBoxes.Count,
                    "Real bar navigation body diverged from collider/BBSIS pieces.");
                Vector3 destination = binding.Spots[0].WaiterServicePoint.position;
                float halfExtent = Math.Abs(manifest.barCounter.openingDirection.x) > 0.5f
                    ? manifest.barCounter.finalSizeMeters.x * 0.5f : manifest.barCounter.finalSizeMeters.z * 0.5f;
                Vector3 origin = root.transform.TransformPoint(manifest.barCounter.waiterLocalPosition +
                    manifest.barCounter.openingDirection * (halfExtent + 2f));
                List<Vector3> route = new List<Vector3>();
                Require(navigation.TryBuildRoute("savic.source.bar.waiter", BistroBuilderNavigationAgentMask.Waiter,
                    origin, destination, 0.28f, route, out float length, out BistroBuilderNavigationRouteKind kind) &&
                    kind != BistroBuilderNavigationRouteKind.DirectDegraded && route.Count > 0,
                    "No real native waiter route reached the verified bar's service point.");
                List<RestaurantPlacementShape> physical = new List<RestaurantPlacementShape>();
                Require(BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(root.GetComponent<RestaurantPlacementFootprint>(),
                    root.transform.position, root.transform.rotation, physical, out error), error);
                List<RestaurantPlacementShape> agent = new List<RestaurantPlacementShape>();
                Vector3 previous = origin;
                foreach (Vector3 next in route)
                {
                    int steps = Math.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, next) / 0.05f));
                    for (int step = 0; step <= steps; step++)
                    {
                        agent.Clear(); agent.Add(new RestaurantPlacementShape(Vector3.Lerp(previous, next, step / (float)steps),
                            Vector3.right, Vector3.forward, Vector2.one * 0.28f, 0));
                        Require(BistroBuilderPhysicalPlacementGeometry.EvaluateConflict(physical, agent) == RestaurantPlacementConflictType.None,
                            "Native waiter route crossed the real bar's physical body.");
                    }
                    previous = next;
                }
                bars.ReleaseGroup(group); adapter.ReleaseCustomerLease();
                Require(body.CanDeactivate(out error), error);
                placeables.UnregisterPlaceable(placeable);
                Require(spatial.SubjectCount == 0 && bars.RegisteredSpotCount == 0, "Real source bar removal retained native registrations.");
                Debug.Log("[SAVIC] REAL BAR NATIVE SERVICE AND ROUTE - PASS: native allocation/lease, occupied guard, " +
                    "root/body identities, " + physical.Count + " actual parts, waiter route=" + kind + ", length=" + length.ToString("F3") +
                    "m sampled every 5cm with 0.28m agent half-width, cleanup. Catalog creation and SaveGame remain pending.");
            }
            finally
            {
                placeables.UnregisterPlaceable(placeable); binding.ReleaseRuntimeRegistration();
                Object.DestroyImmediate(customerObject); Object.DestroyImmediate(services);
            }
        }
        private static SavicManifest MakeManifest(GameObject root) => new SavicManifest {
            source = new SavicSourceRecord { sourceHash = new string('a', 64), originalFileName = "curved_bar.glb", sourceKind = "Model3D" },
            classification = new SavicClassificationRecord { type = "BarCounter", score = 0.95f },
            model3D = SavicModelAnalyzer.Analyze(root, SavicModelAnalysisMode.GenericStatic) };
        private static GameObject MakeU(float wall)
        {
            GameObject root = new GameObject("Open counter body");
            AddBox(root, new Vector3(wall, 1, 4), new Vector3(-2 + wall * 0.5f, 0.5f, 0));
            AddBox(root, new Vector3(wall, 1, 4), new Vector3(2 - wall * 0.5f, 0.5f, 0));
            AddBox(root, new Vector3(4 - 2 * wall, 1, 0.5f), new Vector3(0, 0.5f, 1.75f));
            return root;
        }
        private static void AddBox(GameObject root, Vector3 size, Vector3 center)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.transform.SetParent(root.transform, false); part.transform.localScale = size; part.transform.localPosition = center;
        }
        private static bool HasHash(string path, string hash) => File.Exists(path) &&
            string.Equals(SavicHashService.ComputeSha256(path), hash, StringComparison.OrdinalIgnoreCase);
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        [Serializable] private sealed class Report { public string generatedUtc; public List<Entry> entries = new List<Entry>(); }
        [Serializable] private sealed class Entry { public string savicId, subject; public SavicBarCounterAuthoringRecord plan; }
    }
}
