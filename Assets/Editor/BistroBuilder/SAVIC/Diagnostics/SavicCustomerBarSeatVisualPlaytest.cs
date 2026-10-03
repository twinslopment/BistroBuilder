using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    /// <summary>Real native source/customer presentation probe. Does not grant stool publication.</summary>
    [InitializeOnLoad]
    public static class SavicCustomerBarSeatVisualPlaytest
    {
        private const string Key = "SAVIC.CustomerBarSeatVisual.";
        private const string ProfilePath = "Assets/Data/Restaurant/SAVIC/CustomerHumanoidProfile_Standard.asset";
        private const string ModelPath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";
        private static GameObject services, barRoot, stoolRoot, customerRoot;
        private static RestaurantPlaceableRegistry placeables;
        private static BistroBuilderBarServiceRegistry bars;
        private static BistroBuilderSpatialInteractionService spatial;
        private static BistroBuilderSpatialPlacementAssessmentService assessment;
        private static RestaurantPlaceableItemDefinition temporaryItem;
        private static BistroBuilderCustomerBarSeatPresenter presenter;
        private static BistroBuilderBarServiceSpot spot;
        private static CustomerGroup group;
        private static CustomerMovementView movement;
        private static Vector3 logicalArrival;
        private static Quaternion logicalRotation;
        private static double deadline, holdUntil;
        private static int stage, index, completed;
        private static int initialSubjects, initialLeaseCount;
        private static bool preparedLayout;
        private static bool candidateAcceptance;
        private static bool publishedAcceptance, catalogConfigured;
        private static bool revalidatingPublishedCandidates;
        private static BistroBuilderSaveGameService saveGame;
        private static RestaurantPlaceableCatalogDefinition candidateCatalog;
        private static int diagnosticSlot = -1, loadCount, barUnityId, stoolUnityId;
        private static string barInstanceId, stoolInstanceId, currentSavicId;
        private static bool runFailed;
        private static string[] ids;

        static SavicCustomerBarSeatVisualPlaytest()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }
        public static void RunFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play Mode already active.");
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Require(model != null, "Certified Humanoid source missing.");
            var profile = AssetDatabase.LoadAssetAtPath<BistroBuilderCustomerHumanoidProfile>(ProfilePath);
            if (profile == null)
            { profile = ScriptableObject.CreateInstance<BistroBuilderCustomerHumanoidProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
            profile.ConfigureForEditor(model, 1f, 0.10f);
            Require(profile.ValidateConfiguration(out string error), error);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            SessionState.SetBool(Key + "Active", true); SessionState.SetInt(Key + "Errors", 0);
            SessionState.SetBool(Key + "Success", false); SessionState.SetString(Key + "Error", string.Empty);
            EditorApplication.EnterPlaymode();
        }
        public static void RunCandidateAcceptanceFromCommandLine()
        {
            SessionState.SetBool(Key + "RevalidatePublishedCandidates", false);
            SessionState.SetBool(Key + "Published", false);
            var context = SavicEditorContext.Instance;
            foreach (var m in context.Manifests.GetAll().Where(m => m?.type == "BarStool" && m.status == "NEEDS_REVIEW"))
            {
                string archive = context.Layout.GetArchivedSourcePath(m.source.sourceHash, m.source.originalFileName);
                string mirror = context.Layout.GetUnitySourceMirrorPath(m.source.sourceHash, m.source.originalFileName);
                Require(SavicHashService.ComputeSha256(archive) == m.source.sourceHash && SavicHashService.ComputeSha256(mirror) == m.source.sourceHash,
                    "Candidate refresh source SHA mismatch.");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
                new SavicBarStoolFamilyModule(context.Layout, context.Manifests).PrepareCandidate(m, source);
            }
            SessionState.SetBool(Key + "Candidate", true);
            RunFromCommandLine();
        }
        public static void RunPublishedAcceptanceFromCommandLine()
        {
            SessionState.SetBool(Key + "RevalidatePublishedCandidates", false);
            SessionState.SetBool(Key + "Published", true);
            SessionState.SetBool(Key + "Candidate", true);
            RunFromCommandLine();
        }
        // Re-run the existing candidate acceptance before the strict main-catalog
        // gate when only a shared prefab dependency changed. Production and strict
        // published gates still require Matches; no job/catalog/state is changed.
        public static void RevalidatePublishedCandidatesFromCommandLine()
        {
            var context = SavicEditorContext.Instance;
            foreach (var m in context.Manifests.GetAll().Where(m => m?.type == "BarStool" && m.status == "PUBLISHED"))
            {
                var proof = m.barStoolRuntime;
                Require(proof != null && proof.verifierVersion == SavicBarStoolRuntimeAcceptance.Version &&
                    proof.creationPassed && proof.associationPassed && proof.routePassed && proof.leasePassed && proof.seatedAnimationPassed &&
                    proof.saveLoadPassed && proof.repeatedLoadPassed && proof.cleanupPassed && proof.consoleClean &&
                    proof.sourceHash == m.source.sourceHash && proof.planFingerprint == m.barStool.inputFingerprint &&
                    SavicBarStoolFunctionAdapter.PlanMatches(m) &&
                    proof.reportRelativePath == context.Layout.ToProjectRelativePath(System.IO.Path.Combine(context.Layout.LogsRoot,
                        "savic-barstool-candidate-" + m.savicId + "-runtime-playtest.txt")) &&
                    System.IO.File.Exists(context.Layout.FromProjectRelativePath(proof.reportRelativePath)) &&
                    SavicHashService.ComputeSha256(context.Layout.FromProjectRelativePath(proof.reportRelativePath)) == proof.reportHash &&
                    AssetDatabase.GetAssetDependencyHash(SavicBarStoolRuntimeAcceptance.CustomerPrefabPath).ToString() == proof.customerPrefabDependencyHash &&
                    AssetDatabase.GetAssetDependencyHash(SavicBarStoolRuntimeAcceptance.AnimationCatalogPath).ToString() == proof.animationCatalogDependencyHash &&
                    new SavicBarStoolFunctionAdapter().Validate(AssetDatabase.LoadAssetAtPath<GameObject>(m.genericPlaceable.prefabAssetPath), m, out _),
                    "Published candidate revalidation requires intact source/plan/profile/report/customer/Animation and current physical authoring.");
                Debug.Log("[SAVIC] Published candidate dependency revalidation: " + m.savicId + ", previous=" + proof.prefabDependencyHash +
                    ", current=" + AssetDatabase.GetAssetDependencyHash(m.genericPlaceable.prefabAssetPath) + ". Runtime assertions remain required.");
            }
            SessionState.SetBool(Key + "Published", false);
            SessionState.SetBool(Key + "Candidate", true);
            SessionState.SetBool(Key + "RevalidatePublishedCandidates", true);
            RunFromCommandLine();
        }
        public static void InstallVerifiedCanonicalProfileFromCommandLine()
        {
            string report = File.ReadAllText(Path.Combine(SavicEditorContext.Instance.Layout.LogsRoot, "customer-bar-seat-real-visual-playtest.txt"));
            Require(report.Contains("PASS: three real source stools") && report.Contains("Console errors=0"),
                "Real source visual acceptance is required before changing the canonical customer prefab.");
            var profile = AssetDatabase.LoadAssetAtPath<BistroBuilderCustomerHumanoidProfile>(ProfilePath);
            Require(profile != null && profile.ValidateConfiguration(out _), "Verified Humanoid profile unavailable.");
            const string prefabPath = "Assets/Prefabs/Customers/CustomerGroupPrefab.prefab";
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var members = contents.GetComponent<BistroBuilderAdvancedCustomerMemberVisualGroup>();
                Require(members != null && (members.HumanoidProfile == null || members.HumanoidProfile == profile),
                    "Canonical customer prefab has incompatible manual visual authoring.");
                members.ConfigureHumanoidForEditor(profile);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
            RunFromCommandLine();
        }
        private static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key + "Active", false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0; index = completed = 0; preparedLayout = false;
                candidateAcceptance = SessionState.GetBool(Key + "Candidate", false);
                publishedAcceptance = SessionState.GetBool(Key + "Published", false);
                revalidatingPublishedCandidates = SessionState.GetBool(Key + "RevalidatePublishedCandidates", false);
                catalogConfigured = false;
                runFailed = false;
                ids = SavicEditorContext.Instance.Manifests.GetAll().Where(m => m?.classification?.type == "BarStool" && m.status == (publishedAcceptance || revalidatingPublishedCandidates ? "PUBLISHED" : "NEEDS_REVIEW"))
                    .Select(m => m.savicId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                Require(ids.Length == 3, "Three actual stools are required for complete acceptance.");
                foreach (string id in ids) SessionState.EraseString(Key + "Proof." + id);
                deadline = EditorApplication.timeSinceStartup + 120;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                bool success = SessionState.GetBool(Key + "Success", false) && SessionState.GetInt(Key + "Errors", 0) == 0;
                SessionState.SetBool(Key + "Active", false);
                bool acceptedCandidates = SessionState.GetBool(Key + "Candidate", false);
                bool acceptedPublished = SessionState.GetBool(Key + "Published", false);
                bool acceptedRevalidation = SessionState.GetBool(Key + "RevalidatePublishedCandidates", false);
                SessionState.SetBool(Key + "Candidate", false);
                SessionState.SetBool(Key + "Published", false);
                SessionState.SetBool(Key + "RevalidatePublishedCandidates", false);
                string message = success ? "PASS: three real source stools; native occupancy, actual customer prefab and Navigation arrival, " +
                    "lease-gated sit/idle/stand through Animation V1, bent Humanoid legs/seat pelvis alignment, unchanged logical root, release and cleanup. " +
                    "No stool publication or Play Mode SaveGame claimed."
                    : "FAIL: " + SessionState.GetString(Key + "Error", "Unknown failure.");
                if (success && acceptedCandidates)
                {
                    int stamped = 0;
                    foreach (var manifest in SavicEditorContext.Instance.Manifests.GetAll().Where(m => m?.type == "BarStool" && m.status == (acceptedPublished || acceptedRevalidation ? "PUBLISHED" : "NEEDS_REVIEW")))
                    {
                        string proofText = SessionState.GetString(Key + "Proof." + manifest.savicId, string.Empty);
                        Require(!string.IsNullOrEmpty(proofText), "Complete repeated runtime proof is missing.");
                        var proof = JsonUtility.FromJson<SavicBarStoolRuntimeAcceptanceRecord>(proofText);
                        string reportPath = Path.Combine(SavicEditorContext.Instance.Layout.LogsRoot,
                            "savic-barstool-candidate-" + manifest.savicId + "-runtime-playtest.txt");
                        File.WriteAllText(reportPath, "PASS: canonical " + (acceptedPublished ? "main catalog" : "candidate") + " creation/automatic association, actual customer Navigation arrival, native occupancy/lease, " +
                            "Humanoid sit/idle/stand, stable bar/stool IDs with new Unity instances through two real SaveGame loads, reallocated customer after each load, slot deleted, Console clean through Editor.\n" +
                            "Save checkpoints are unoccupied; active service-session recovery is not claimed.\n" + DateTime.UtcNow.ToString("O"));
                        proof.cleanupPassed = proof.consoleClean = true;
                        proof.reportRelativePath = SavicEditorContext.Instance.Layout.ToProjectRelativePath(reportPath);
                        proof.reportHash = SavicHashService.ComputeSha256(reportPath); proof.verifiedUtc = DateTime.UtcNow.ToString("O");
                        manifest.barStoolRuntime = proof; SavicEditorContext.Instance.Manifests.Save(manifest);
                        Require(SavicBarStoolRuntimeAcceptance.Matches(manifest, SavicEditorContext.Instance.Layout), "Stool runtime acceptance is not current.");
                        stamped++;
                    }
                    Require(stamped == 3, "Runtime acceptance must stamp exactly the three fully tested sources.");
                    message = "PASS: three canonical " + (acceptedPublished ? "main catalog items" : "candidates") + ", native seated customers, two real SaveGame loads each, fresh instances/stable links, diagnostic slot deleted, Console clean.";
                }
                File.WriteAllText(Path.Combine(SavicEditorContext.Instance.Layout.LogsRoot, "customer-bar-seat-real-visual-playtest.txt"),
                    DateTime.UtcNow.ToString("O") + "\n" + message + "\nConsole errors=" + SessionState.GetInt(Key + "Errors", 0));
                if (success) Debug.Log("[SAVIC] CUSTOMER BAR SEAT VISUAL PLAYTEST - " + message);
                else Debug.LogError("[SAVIC] CUSTOMER BAR SEAT VISUAL PLAYTEST - " + message);
                EditorApplication.Exit(success ? 0 : 1);
            }
        }
        private static void Log(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Key + "Active", false) || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
            SessionState.SetString(Key + "Error", message);
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "Active", false) || !EditorApplication.isPlaying) return;
            try
            {
                if (runFailed)
                {
                    if (saveGame != null && saveGame.IsBusy) return;
                    try { Cleanup(); } catch { /* Preserve the first cause and still remove the diagnostic slot. */ }
                    if (candidateAcceptance && diagnosticSlot >= 0 && saveGame != null && saveGame.SlotExists(diagnosticSlot))
                    { stage = 8; if (saveGame.TryDeleteSlot(diagnosticSlot, out _)) return; }
                    EditorApplication.ExitPlaymode(); return;
                }
                Require(EditorApplication.timeSinceStartup < deadline, "Visual test timed out at stage " + stage + ": " + presenter?.LastError);
                Require(SessionState.GetInt(Key + "Errors", 0) == 0, SessionState.GetString(Key + "Error", "Console error."));
                if (stage == 0 && Time.frameCount >= 5)
                { Require(ids?.Length == 3, "Expected the three real pending stool sources."); Build(ids[index]); stage = 1; }
                else if (stage == 1 && movement.HasReachedDestination)
                {
                    logicalArrival = group.transform.position; logicalRotation = group.transform.rotation;
                    holdUntil = EditorApplication.timeSinceStartup + 0.5; stage = 2;
                }
                else if (stage == 2 && EditorApplication.timeSinceStartup >= holdUntil)
                {
                    Require(spot.GetComponent<BistroBuilderBarSpatialAdapter>().HasCustomerLease,
                        "Native operational coordinator did not grant the actual customer lease.");
                    stage = 3;
                }
                else if (stage == 3 && presenter.State == BistroBuilderCustomerBarSeatPresenter.VisualState.Seated)
                { holdUntil = EditorApplication.timeSinceStartup + 0.5; stage = 4; }
                else if (stage == 4 && EditorApplication.timeSinceStartup >= holdUntil)
                {
                    ValidateSeated(); Capture(ids[index]);
                    Require(bars.ReleaseGroup(group), "Native customer release failed.");
                    spot.GetComponent<BistroBuilderBarSpatialAdapter>().ReleaseCustomerLease(); stage = 5;
                }
                else if (stage == 5 && presenter.State == BistroBuilderCustomerBarSeatPresenter.VisualState.Baseline)
                {
                    Require(Vector3.Distance(group.transform.position, logicalArrival) < 0.0001f &&
                        Quaternion.Angle(group.transform.rotation, logicalRotation) < 0.001f,
                        "Animation stand changed Navigation's logical root.");
                    Require(presenter.AlignedSeat == null && spot.IsFree && spatial.ActiveLeaseCount == 0,
                        "Standing retained a native occupant or lease.");
                    Debug.Log("[SAVIC] REAL CUSTOMER BAR SEAT VISUAL - PASS source=" + ids[index] + "; root=" + logicalArrival);
                    if (candidateAcceptance && loadCount < 2)
                    {
                        Object.DestroyImmediate(customerRoot); customerRoot = null; presenter = null;
                        stage = 6; deadline = EditorApplication.timeSinceStartup + 120;
                        if (loadCount == 0) Require(saveGame.TrySaveSlot(diagnosticSlot, "SAVIC native bar seat candidate", out string error), error);
                        else Require(saveGame.TryLoadSlot(diagnosticSlot, out string error), error);
                        return;
                    }
                    if (candidateAcceptance) SaveProofBeforeCleanup();
                    Cleanup(); completed++; index++;
                    if (index < ids.Length) { stage = 0; deadline = EditorApplication.timeSinceStartup + 90; }
                    else
                    {
                        Require(completed == 3, "Incomplete source tests.");
                        if (candidateAcceptance)
                        { stage = 8; Require(saveGame.TryDeleteSlot(diagnosticSlot, out string error), error); }
                        else { SessionState.SetBool(Key + "Success", true); EditorApplication.ExitPlaymode(); }
                    }
                }
            }
            catch (Exception error)
            {
                SessionState.SetString(Key + "Error", error.ToString()); SessionState.SetBool(Key + "Success", false);
                runFailed = true;
            }
        }
        private static void Build(string id)
        {
            var context = SavicEditorContext.Instance;
            var manifest = context.Manifests.GetAll().First(m => m.savicId == id);
            string sourcePath = context.Layout.GetArchivedSourcePath(manifest.source.sourceHash, manifest.source.originalFileName);
            string mirror = context.Layout.GetUnitySourceMirrorPath(manifest.source.sourceHash, manifest.source.originalFileName);
            Require(SavicHashService.ComputeSha256(sourcePath) == manifest.source.sourceHash &&
                SavicHashService.ComputeSha256(mirror) == manifest.source.sourceHash, "Real stool source SHA mismatch.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(context.Layout.ToProjectRelativePath(mirror));
            var clone = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(manifest));
            clone.model3D = SavicModelAnalyzer.Analyze(source, SavicModelAnalysisMode.GenericStatic);
            Require(SavicBarStoolAuthoringPlanner.TryPlan(clone, source, SavicBarStoolAuthoringPlanner.GetOrCreateProfile(), out var plan, out string error), error);
            if (revalidatingPublishedCandidates)
                Require(plan.inputFingerprint == manifest.barStool.inputFingerprint,
                    "Candidate revalidation source recomputation disagrees with the stored physical plan.");
            clone.barStool = plan; clone.genericPlaceable.requiresFunctionalAdapter = true;
            clone.genericPlaceable.integrationMode = SavicBarStoolFunctionAdapter.Mode;
            placeables = Object.FindFirstObjectByType<RestaurantPlaceableRegistry>();
            bars = Object.FindFirstObjectByType<BistroBuilderBarServiceRegistry>();
            spatial = Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
            assessment = Object.FindFirstObjectByType<BistroBuilderSpatialPlacementAssessmentService>();
            var animation = Object.FindFirstObjectByType<BistroBuilderCharacterAnimationServiceV1>();
            Require(placeables != null && bars != null && spatial != null && assessment != null && animation != null,
                "The actual Prototype authorities are unavailable.");
            var lifecycle = Object.FindFirstObjectByType<RestaurantPlaceableLifecycleService>();
            var edit = Object.FindFirstObjectByType<RestaurantEditModeService>();
            var creation = Object.FindFirstObjectByType<RestaurantPlaceableCreationService>();
            Require(edit.TryEnterEditMode(out _, out error), error);
            if (!preparedLayout)
            {
                foreach (var furniture in placeables.RegisteredPlaceables.Where(p => p != null &&
                             (p.GetComponent<RestaurantSeat>() != null || p.GetComponent<RestaurantTable>() != null))
                             .OrderBy(p => p.GetComponent<RestaurantSeat>() != null ? 0 : 1).ToArray())
                    Require(lifecycle.TryDeactivateInstance(furniture, out _, out _), "Temporary furniture lifecycle removal failed.");
                spatial.RebuildSubjects(); Object.FindFirstObjectByType<BistroBuilderNavigationService>()?.RebuildNavigationTopology();
                preparedLayout = true;
            }
            initialSubjects = spatial.SubjectCount; initialLeaseCount = spatial.ActiveLeaseCount;
            var barManifest = context.Manifests.GetAll().First(m => m.type == "BarCounter" && m.status == "PUBLISHED");
            if (candidateAcceptance && !catalogConfigured) ConfigureCandidateCatalog();
            var barPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(barManifest.genericPlaceable.prefabAssetPath);
            RestaurantPlaceableObject placedBar = null;
            var barItem = barPrefab.GetComponent<RestaurantPlaceableObject>().ItemDefinition;
            foreach (var area in Object.FindObjectsByType<RestaurantArea>(FindObjectsSortMode.None).OrderBy(a => a.AreaId))
            {
                if (area.BoundaryColliders == null) continue;
                foreach (var boundary in area.BoundaryColliders)
                {
                    if (boundary == null) continue;
                    Bounds bounds = boundary.bounds;
                    var poses = new System.Collections.Generic.List<Vector3>();
                    for (float x = bounds.min.x + 0.75f; x < bounds.max.x - 0.75f; x += 0.8f)
                    for (float z = bounds.min.z + 0.75f; z < bounds.max.z - 0.75f; z += 0.8f)
                        poses.Add(new Vector3(x, 0, z));
                    foreach (var pose in poses.OrderBy(p => (p - bounds.center).sqrMagnitude))
                    {
                        if (!creation.TryBeginCreation(barItem, pose, Quaternion.identity, null, out var candidate, out _)) continue;
                        if (creation.TryCommitActiveCreation(out _)) placedBar = candidate;
                        else creation.TryCancelActiveCreation(out _);
                        if (placedBar != null) break;
                    }
                    if (placedBar != null) break;
                }
                if (placedBar != null) break;
            }
            Require(placedBar != null, "No canonical pose accepted the actual published bar.");
            barRoot = placedBar.gameObject;
            var bar = barRoot.GetComponent<BistroBuilderBarPlaceableBinding>();
            spot = bar.Spots[0];
            if (candidateAcceptance)
            {
                var stoolItem = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(manifest.genericPlaceable.itemDefinitionAssetPath);
                Quaternion stoolRotation = Quaternion.LookRotation(spot.CustomerPoint.forward);
                Vector3 stoolPosition = spot.CustomerPoint.position - stoolRotation * new Vector3(plan.seatLocalPosition.x, 0, plan.seatLocalPosition.z);
                Require(creation.TryBeginCreation(stoolItem, stoolPosition, stoolRotation, null, out var candidate, out var begin), begin.Message);
                Require(creation.TryCommitActiveCreation(out var commit), commit.Message + " " + commit.ValidationResult.TechnicalMessage);
                stoolRoot = candidate.gameObject;
                barInstanceId = placedBar.InstanceId; stoolInstanceId = candidate.InstanceId;
                barUnityId = barRoot.GetInstanceID(); stoolUnityId = stoolRoot.GetInstanceID(); loadCount = 0; currentSavicId = id;
            }
            else
            {
            stoolRoot = new GameObject("Actual source stool visual probe"); stoolRoot.SetActive(false);
            stoolRoot.AddComponent<RestaurantPlaceableObject>();
            var visual = new GameObject("Visual").transform; visual.SetParent(stoolRoot.transform, false);
            var imported = Object.Instantiate(source, visual, false);
            SavicGenericPlaceablePublisher.NormalizeSourceVisual(imported.transform, clone.model3D);
            new SavicBarStoolFunctionAdapter().Apply(stoolRoot, clone);
            temporaryItem = ScriptableObject.CreateInstance<RestaurantPlaceableItemDefinition>();
            var serialized = new SerializedObject(temporaryItem); serialized.FindProperty("itemId").stringValue = "visual_stool_" + id;
            serialized.ApplyModifiedPropertiesWithoutUndo(); stoolRoot.GetComponent<RestaurantPlaceableObject>().SetItemDefinition(temporaryItem);
            Quaternion rotation = Quaternion.LookRotation(spot.CustomerPoint.forward);
            stoolRoot.transform.SetPositionAndRotation(spot.CustomerPoint.position - rotation * new Vector3(plan.seatLocalPosition.x, 0, plan.seatLocalPosition.z), rotation);
            stoolRoot.GetComponent<BistroBuilderBarSeatBinding>().ConfigureDependenciesForEditor(placeables, bars, spatial, assessment);
            stoolRoot.GetComponent<RestaurantPlaceableObject>().AssignInstanceId("visual_stool_" + id); stoolRoot.SetActive(true);
            Require(placeables.RegisterPlaceable(stoolRoot.GetComponent<RestaurantPlaceableObject>()), placeables.LastRegistrationError);
            }
            Require(edit.TryExitEditMode(true, out _), "Temporary bar layout could not exit Edit Mode.");
            CreateCustomer();
        }
        private static void CreateCustomer()
        {
            string error = string.Empty;
            customerRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Customers/CustomerGroupPrefab.prefab"));
            foreach (var behaviour in customerRoot.GetComponents<MonoBehaviour>())
                if (!(behaviour is CustomerGroup) && !(behaviour is CustomerMovementView) && !(behaviour is BistroBuilderAdvancedCustomerMemberVisualGroup)) behaviour.enabled = false;
            group = customerRoot.GetComponent<CustomerGroup>();
            Require(group.Initialize(99981, 1, BistroBuilderServiceMode.WaitingAtBar) &&
                bars.TryRestoreGroupAllocation(group, spot.BarSpotId, new[] { spot.BarSpotId }, BistroBuilderServiceMode.WaitingAtBar, out error),
                "Actual customer group did not receive native stool occupancy.");
            customerRoot.transform.position = spot.CustomerApproachPoint.position + spot.CustomerPoint.forward * 0.8f;
            var members = customerRoot.GetComponent<BistroBuilderAdvancedCustomerMemberVisualGroup>();
            Require(members != null, "Canonical member visual group missing on actual customer prefab.");
            if (members.HumanoidProfile == null)
                members.ConfigureHumanoidForEditor(AssetDatabase.LoadAssetAtPath<BistroBuilderCustomerHumanoidProfile>(ProfilePath));
            Require(members.EnsureVisuals(), "Actual customer members were not materialized.");
            presenter = customerRoot.GetComponentInChildren<BistroBuilderCustomerBarSeatPresenter>();
            movement = customerRoot.GetComponent<CustomerMovementView>();
            Require(movement.MoveToBarPoint(spot), "Actual movement view rejected bar approach.");
            Require(presenter != null && presenter.Animator.isHuman, "Actual customer member lacks a Humanoid presenter.");
            Require(presenter.State == BistroBuilderCustomerBarSeatPresenter.VisualState.Baseline && presenter.AlignedSeat == null,
                "Member sat before actual Navigation arrival.");
        }
        private static void ConfigureCandidateCatalog()
        {
            var catalog = Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>();
            var definitions = Object.FindFirstObjectByType<BistroBuilderSaveDefinitionCatalog>();
            Require(catalog?.CatalogDefinition != null && definitions != null, "Actual candidate catalog authorities unavailable.");
            if (!publishedAcceptance)
            {
            candidateCatalog = Object.Instantiate(catalog.CatalogDefinition);
            var serialized = new SerializedObject(candidateCatalog); var items = serialized.FindProperty("items");
            foreach (string id in ids)
            {
                var m = SavicEditorContext.Instance.Manifests.GetAll().First(m => m.savicId == id);
                bool present = false;
                for (int index = 0; index < items.arraySize; index++)
                    if (items.GetArrayElementAtIndex(index).objectReferenceValue is RestaurantPlaceableItemDefinition existing &&
                        existing.ItemId == m.canonicalContentId) { present = true; break; }
                if (present) continue;
                items.arraySize++; items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(m.genericPlaceable.itemDefinitionAssetPath);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var catalogFields = new SerializedObject(catalog); catalogFields.FindProperty("catalogDefinition").objectReferenceValue = candidateCatalog;
            catalogFields.ApplyModifiedPropertiesWithoutUndo(); catalog.RebuildCatalog();
            var definitionFields = new SerializedObject(definitions); var sources = definitionFields.FindProperty("sourceCatalogs");
            sources.arraySize++; sources.GetArrayElementAtIndex(sources.arraySize - 1).objectReferenceValue = candidateCatalog;
            definitionFields.ApplyModifiedPropertiesWithoutUndo(); definitions.RebuildIndex();
            }
            else
            {
                foreach (string id in ids)
                {
                    var m = SavicEditorContext.Instance.Manifests.GetAll().First(m => m.savicId == id);
                    var expected = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(m.genericPlaceable.itemDefinitionAssetPath);
                    Require(m.status == "PUBLISHED" && m.genericPlaceableReadiness.catalogResolvable &&
                        SavicBarStoolRuntimeAcceptance.Matches(m, SavicEditorContext.Instance.Layout) &&
                        AssetDatabase.GetAssetPath(catalog.CatalogDefinition) == "Assets/Data/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalog_Main.asset" &&
                        catalog.TryGetItem(expected.ItemId, out var catalogItem) && catalogItem == expected &&
                        definitions.TryGetDefinition(expected.ItemId, out var savedItem) && savedItem == expected,
                        "Actual main catalog/save definitions/current proof do not resolve the published item.");
                }
            }
            catalogConfigured = true;
            saveGame = Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
            Require(saveGame != null, "Real SaveGame service unavailable.");
            for (int slot = 960; slot < 980; slot++) if (!saveGame.SlotExists(slot)) { diagnosticSlot = slot; break; }
            Require(diagnosticSlot >= 0, "No free diagnostic slot.");
            saveGame.OperationCompleted += Saved;
        }
        private static void Saved(BistroBuilderSaveOperationResult result)
        {
            if (!SessionState.GetBool(Key + "Active", false) || !candidateAcceptance) return;
            try
            {
                Require(result != null && result.Succeeded, "Real SaveGame failed: " + result?.Message);
                if (stage == 8)
                {
                    Require(!saveGame.SlotExists(diagnosticSlot), "Diagnostic slot was not deleted.");
                    saveGame.OperationCompleted -= Saved; SessionState.SetBool(Key + "Success", !runFailed && completed == 3); EditorApplication.ExitPlaymode(); return;
                }
                if (stage != 6) return;
                if (result.OperationKind == BistroBuilderSaveOperationKind.Save)
                { Require(saveGame.TryLoadSlot(diagnosticSlot, out string error), error); return; }
                var restoredBar = placeables.RegisteredPlaceables.FirstOrDefault(p => p.InstanceId == barInstanceId);
                var restoredStool = placeables.RegisteredPlaceables.FirstOrDefault(p => p.InstanceId == stoolInstanceId);
                Require(restoredBar != null && restoredStool != null && restoredBar.gameObject.GetInstanceID() != barUnityId &&
                    restoredStool.gameObject.GetInstanceID() != stoolUnityId, "SaveGame did not reconstruct both placeables into new Unity instances.");
                barRoot = restoredBar.gameObject; stoolRoot = restoredStool.gameObject;
                spot = barRoot.GetComponent<BistroBuilderBarPlaceableBinding>().Spots[0];
                Require(stoolRoot.GetComponent<BistroBuilderBarSeatBinding>().AttachedSpot == spot && spot.IsFree &&
                    spot.BarSpotId == BistroBuilderBarPlaceableBinding.BuildSpotId(barInstanceId, 0), "Real loaded stool link/spot ID is incorrect.");
                barUnityId = barRoot.GetInstanceID(); stoolUnityId = stoolRoot.GetInstanceID(); loadCount++;
                CreateCustomer(); stage = 1; deadline = EditorApplication.timeSinceStartup + 90;
            }
            catch (Exception error)
            { SessionState.SetString(Key + "Error", error.ToString()); SessionState.SetBool(Key + "Success", false); runFailed = true; }
        }
        private static void SaveProofBeforeCleanup()
        {
            var m = SavicEditorContext.Instance.Manifests.GetAll().First(m => m.savicId == currentSavicId);
            var p = new SavicBarStoolRuntimeAcceptanceRecord {
                verifierVersion = SavicBarStoolRuntimeAcceptance.Version, sourceHash = m.source.sourceHash, planFingerprint = m.barStool.inputFingerprint,
                prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(m.genericPlaceable.prefabAssetPath).ToString(),
                customerPrefabDependencyHash = AssetDatabase.GetAssetDependencyHash(SavicBarStoolRuntimeAcceptance.CustomerPrefabPath).ToString(),
                animationCatalogDependencyHash = AssetDatabase.GetAssetDependencyHash(SavicBarStoolRuntimeAcceptance.AnimationCatalogPath).ToString(),
                creationPassed = true, associationPassed = true, routePassed = true, leasePassed = true, seatedAnimationPassed = true,
                saveLoadPassed = loadCount == 2, repeatedLoadPassed = loadCount == 2 };
            SessionState.SetString(Key + "Proof." + m.savicId, JsonUtility.ToJson(p));
        }
        private static void ValidateSeated()
        {
            var frame = spot.AttachedSeat.SeatFrame;
            float pelvisError = Vector3.Distance(presenter.Hips.position, frame.position + frame.up * 0.10f);
            Require(pelvisError < 0.025f && presenter.CurrentMotionId == "seat.idle.standard", "Idle did not align actual Humanoid pelvis with authored seat: " + pelvisError);
            foreach (bool left in new[] { true, false })
            {
                var upper = presenter.Animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                var knee = presenter.Animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                var foot = presenter.Animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                float horizontal = Mathf.Abs(Vector3.Dot((knee.position - upper.position).normalized, Vector3.up));
                float bend = Vector3.Angle(upper.position - knee.position, foot.position - knee.position);
                Debug.Log("[SAVIC] Humanoid seat measurement " + (left ? "left" : "right") + ": pelvisError=" + pelvisError + ", thighVertical=" + horizontal + ", kneeAngle=" + bend + ", footY=" + foot.position.y);
                Require(horizontal < 0.45f && bend > 55f && bend < 135f && foot.position.y > -0.05f, "Humanoid did not show a physically bent seated leg.");
            }
            foreach (var renderer in presenter.GetComponentsInChildren<Renderer>())
            foreach (var material in renderer.sharedMaterials)
                Require(material != null && material.shader != null && material.shader.name != "Hidden/InternalErrorShader",
                    "Actual Humanoid member has missing or error materials.");
            Require(Vector3.Distance(group.transform.position, logicalArrival) < 0.0001f && Quaternion.Angle(group.transform.rotation, logicalRotation) < 0.001f &&
                spot.AssignedCustomerGroup == group && spatial.ActiveLeaseCount == 1, "Animation changed logical navigation, occupancy or leases.");
        }
        private static void Capture(string id)
        {
            var root = new GameObject("Seat visual evidence camera");
            var camera = root.AddComponent<Camera>();
            var lightRoot = new GameObject("Seat visual evidence light"); var light = lightRoot.AddComponent<Light>(); light.type = LightType.Directional;
            lightRoot.transform.rotation = Quaternion.Euler(40, -35, 0);
            var target = spot.AttachedSeat.SeatFrame.position + Vector3.up * 0.35f;
            root.transform.position = target + spot.CustomerPoint.right * 2.5f - spot.CustomerPoint.forward * 2f + Vector3.up * 1.5f;
            root.transform.LookAt(target); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.14f, 0.16f, 0.18f);
            var rt = new RenderTexture(800, 800, 24); var pixels = new Texture2D(800, 800, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, 800, 800), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(SavicEditorContext.Instance.Layout.LogsRoot, "customer-seated-" + id + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(pixels); Object.Destroy(root); Object.Destroy(lightRoot); }
        }
        private static void Cleanup()
        {
            if (spot != null) { if (group != null) bars?.ReleaseGroup(group); spot.GetComponent<BistroBuilderBarSpatialAdapter>()?.ReleaseCustomerLease(); }
            if (customerRoot != null) Object.DestroyImmediate(customerRoot);
            if (stoolRoot != null) { placeables?.UnregisterPlaceable(stoolRoot.GetComponent<RestaurantPlaceableObject>()); Object.DestroyImmediate(stoolRoot); }
            if (barRoot != null) { placeables?.UnregisterPlaceable(barRoot.GetComponent<RestaurantPlaceableObject>()); Object.DestroyImmediate(barRoot); }
            if (spatial != null) Require(spatial.SubjectCount == initialSubjects && spatial.ActiveLeaseCount == initialLeaseCount, "Visual fixture retained subjects/leases.");
            if (services != null) Object.DestroyImmediate(services);
            if (temporaryItem != null) Object.DestroyImmediate(temporaryItem);
            customerRoot = stoolRoot = barRoot = services = null; presenter = null; spot = null;
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
