using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [InitializeOnLoad]
    public static class SavicPublishedTableRuntimePlaytest
    {
        private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
        private const string StageKey = "SAVIC.RuntimeTable.Stage";
        private const string ItemPathKey = "SAVIC.RuntimeTable.ItemPath";
        private const string SavicIdKey = "SAVIC.RuntimeTable.SavicId";
        private const string ReportPathKey = "SAVIC.RuntimeTable.ReportPath";
        private const string SuccessKey = "SAVIC.RuntimeTable.Success";
        private const string FamilyKey = "SAVIC.RuntimeTable.Family";
        private const string LampPlanKey = "SAVIC.RuntimeTable.LampPlan";
        private const string BarManifestKey = "SAVIC.RuntimeTable.BarManifest";
        private const string BarCandidateKey = "SAVIC.RuntimeTable.BarCandidate";
        private const string RuntimeErrorsKey = "SAVIC.RuntimeTable.Errors";
        private const string LastRuntimeErrorKey = "SAVIC.RuntimeTable.LastError";
        private static RestaurantPlaceableCatalogDefinition runtimeCatalog;

        private static BistroBuilderSaveGameService saveGame;
        private static RestaurantPlaceableRegistry registry;
        private static RestaurantPlaceableLifecycleService lifecycle;
        private static RestaurantPlaceableCreationService creation;
        private static RestaurantEditModeService editMode;
        private static RestaurantPlaceableObject placed;
        private static RestaurantPlaceableItemDefinition item;
        private static int slot = -1;
        private static int firstUnityInstanceId;
        private static string instanceId = string.Empty;
        private static string resultMessage = string.Empty;
        private static bool resultSuccess;
        private static bool commandLine;
        private static double deadline;

        static SavicPublishedTableRuntimePlaytest()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            Application.logMessageReceived -= OnRuntimeLog;
            Application.logMessageReceived += OnRuntimeLog;
        }

        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Published Table Runtime Playtest", false, 135)]
        public static void RunFromMenu() => Begin(false);

        public static void RunFromCommandLine() => Begin(true);

        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Published Storage Furniture Runtime Playtest", false, 136)]
        public static void RunStorageFurnitureFromMenu() => Begin(false, "StorageFurniture");

        public static void RunStorageFurnitureFromCommandLine() => Begin(true, "StorageFurniture");

        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Published Floor Lamp Runtime Playtest", false, 138)]
        public static void RunFloorLampFromMenu() => Begin(false, "FloorLamp");
        public static void RunFloorLampFromCommandLine() => Begin(true, "FloorLamp");
        public static void RunBarCounterCandidateFromCommandLine() => Begin(true, "BarCounter");
        public static void RunPublishedBarCounterFromCommandLine() => Begin(true, "BarCounter", false);

        internal static void RunSelectedBar(string id, bool candidate) => Begin(false, "BarCounter", candidate, id);

        private static void Begin(bool cli, string family = "Table", bool barCandidate = true, string selectedId = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play Mode is already active.");

            SavicEditorContext context = SavicEditorContext.Instance;
            SavicManifest manifest = context.Manifests.GetAll().FirstOrDefault(candidate =>
                candidate != null && candidate.status == (family == "BarCounter" && barCandidate ? "NEEDS_REVIEW" : "PUBLISHED") &&
                candidate.type == family && (selectedId == null || candidate.savicId == selectedId) &&
                (family == "Table"
                    ? candidate.tableAuthoring?.seatingDefinitionAssetPath == SavicTableAuthoringPlanner.CompactSquareTwoSeatingPath
                    : candidate.genericPlaceable?.integrationMode ==
                        (family == "FloorLamp" ? SavicFloorLampFunctionAdapter.Mode : family == "BarCounter" ? SavicBarCounterFunctionAdapter.Mode : "STATIC_FURNITURE")));
            string itemPath = family == "Table" ? manifest?.tableAuthoring?.itemDefinitionAssetPath
                : manifest?.genericPlaceable?.itemDefinitionAssetPath;
            if (manifest?.source == null ||
                string.IsNullOrWhiteSpace(itemPath))
                throw new InvalidOperationException("No published SAVIC " + family + " is available.");

            string archive = context.Layout.FromProjectRelativePath(
                manifest.source.archivedRelativePath);
            if (!File.Exists(archive) ||
                !string.Equals(SavicHashService.ComputeSha256(archive),
                    manifest.source.sourceHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Published " + family + " original failed SHA-256 verification.");

            SessionState.SetString(ItemPathKey, itemPath);
            SessionState.SetString(FamilyKey, family);
            SessionState.SetBool(BarCandidateKey, family == "BarCounter" && barCandidate);
            SessionState.SetString(BarManifestKey, family == "BarCounter" ? JsonUtility.ToJson(manifest) : string.Empty);
            SessionState.SetString(LampPlanKey, family == "FloorLamp" ? JsonUtility.ToJson(manifest.floorLamp) : string.Empty);
            SessionState.SetString(SavicIdKey, manifest.savicId);
            SessionState.SetString(ReportPathKey,
                Path.Combine(context.Layout.LogsRoot, family == "BarCounter" ? "savic-barcounter-candidate-" + manifest.savicId + "-runtime-playtest.txt"
                    : "savic-published-" + family.ToLowerInvariant() + "-runtime-playtest.txt"));
            SessionState.SetBool(SuccessKey, false);
            SessionState.SetInt(RuntimeErrorsKey, 0);
            SessionState.SetString(LastRuntimeErrorKey, string.Empty);
            SessionState.SetString(StageKey, cli ? "enter_cli" : "enter_menu");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            string stage = SessionState.GetString(StageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(stage))
                return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                commandLine = stage.EndsWith("cli", StringComparison.Ordinal);
                deadline = EditorApplication.timeSinceStartup + 45d;
                SessionState.SetString(StageKey, commandLine ? "init_cli" : "init_menu");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                bool success = SessionState.GetBool(SuccessKey, false);
                string reportPath = SessionState.GetString(ReportPathKey, string.Empty);
                if (SessionState.GetInt(RuntimeErrorsKey, 0) > 0)
                {
                    success = false;
                    File.WriteAllText(reportPath, "FAIL: Runtime or cleanup emitted Error/Exception/Assert: " + SessionState.GetString(LastRuntimeErrorKey, ""));
                }
                if (success && SessionState.GetString(FamilyKey, "Table") == "BarCounter")
                {
                    try { StampBarAcceptance(reportPath); }
                    catch (Exception e) { success = false; File.WriteAllText(reportPath, "FAIL: " + e.Message); }
                }
                bool cli = stage.EndsWith("cli", StringComparison.Ordinal);
                SessionState.EraseString(StageKey);
                if (success) Debug.Log("[SAVIC] " + File.ReadAllText(reportPath) + " Runtime and Editor cleanup Console=clean.");
                else Debug.LogError("[SAVIC] " + File.ReadAllText(reportPath));
                if (SavicRuntimeVerificationSession.IsActive) SavicRuntimeVerificationSession.Complete(success, File.ReadAllText(reportPath));
                else if (cli) EditorApplication.Exit(success ? 0 : 1);
            }
        }

        private static void OnRuntimeLog(string message, string stack, LogType type)
        {
            if (string.IsNullOrEmpty(SessionState.GetString(StageKey, "")) ||
                (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            SessionState.SetInt(RuntimeErrorsKey, SessionState.GetInt(RuntimeErrorsKey, 0) + 1);
            SessionState.SetString(LastRuntimeErrorKey, message);
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            string stage = SessionState.GetString(StageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(stage) || stage.StartsWith("exit_", StringComparison.Ordinal))
                return;

            if (EditorApplication.timeSinceStartup > deadline)
            {
                Finish(false, "Runtime playtest timed out during " + stage + ".");
                return;
            }

            if (stage.StartsWith("init_", StringComparison.Ordinal) && Time.frameCount >= 3)
                Initialize();
            else if (stage.StartsWith("cleanup_", StringComparison.Ordinal) &&
                     (saveGame == null || !saveGame.IsBusy))
                CleanSlotOrExit();
        }

        private static void Initialize()
        {
            saveGame = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
            registry = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>();
            lifecycle = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableLifecycleService>();
            creation = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCreationService>();
            editMode = UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>();
            item = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(
                SessionState.GetString(ItemPathKey, string.Empty));
            if (saveGame == null || registry == null || lifecycle == null ||
                creation == null || editMode == null || item == null ||
                registry.RegisteredPlaceableCount == 0)
                return;

            for (int candidate = 960; candidate <= 979; candidate++)
            {
                if (!saveGame.SlotExists(candidate))
                {
                    slot = candidate;
                    break;
                }
            }
            if (slot < 0)
            {
                Finish(false, "No free diagnostic save slot is available.");
                return;
            }

            if (SessionState.GetString(FamilyKey, "Table") == "BarCounter" && runtimeCatalog == null)
            {
                try
                {
                    if (SessionState.GetBool(BarCandidateKey, false)) ConfigureCandidateCatalog();
                    else
                    {
                        var catalog = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>();
                        var definitions = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveDefinitionCatalog>();
                        if (catalog == null || definitions == null || !catalog.TryGetItem(item.ItemId, out var currentItem) || currentItem != item ||
                            !definitions.TryGetDefinition(item.ItemId, out var currentSaved) || currentSaved != item)
                            throw new InvalidOperationException("Published bar is not resolvable through the actual scene catalog and SaveGame definitions.");
                        Debug.Log("[SAVIC] Published bar resolved from the actual main catalog and SaveGame definitions; no candidate catalog installed.");
                    }
                }
                catch (Exception e) { Finish(false, e.Message); return; }
            }

            if (!editMode.IsEditModeActive &&
                !editMode.TryEnterEditMode(out _, out string editError))
            {
                Finish(false, "Edit Mode could not start: " + editError);
                return;
            }

            RestaurantPlaceableObject referenceTable = registry.RegisteredPlaceables
                .FirstOrDefault(placeable => placeable != null &&
                    placeable.GetComponent<RestaurantTable>() != null &&
                    placeable.PlacementAnchor != null);
            if (referenceTable == null)
            {
                Finish(false, "The restaurant scene has no floor-height table reference.");
                return;
            }

            float floorY = referenceTable.PlacementAnchor.position.y;
            if (SessionState.GetString(FamilyKey, "Table") == "BarCounter")
            {
                try { PrepareBarRuntimeLayout(); }
                catch (Exception e) { Finish(false, e.Message); return; }
            }
            string lastRejection = string.Empty;
            int attempts = 0;
            Dictionary<string, int> placementRejections = new Dictionary<string, int>();
            RestaurantArea[] areas = UnityEngine.Object.FindObjectsByType<RestaurantArea>(
                FindObjectsSortMode.None);
            foreach (RestaurantArea area in areas.OrderBy(candidate =>
                         (candidate.AreaId ?? string.Empty).IndexOf("dining",
                             StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1))
            {
                if (area?.BoundaryColliders == null)
                    continue;
                foreach (Collider boundary in area.BoundaryColliders)
                {
                    if (boundary == null || !boundary.enabled)
                        continue;
                    Bounds bounds = boundary.bounds;
                    List<Vector3> positions = new List<Vector3>();
                    for (float x = bounds.min.x + 0.55f;
                         x <= bounds.max.x - 0.55f; x += 0.85f)
                    {
                        for (float z = bounds.min.z + 0.55f;
                             z <= bounds.max.z - 0.55f; z += 0.85f)
                            positions.Add(new Vector3(x, floorY, z));
                    }

                    foreach (Vector3 anchor in positions.OrderBy(position =>
                                 (position - bounds.center).sqrMagnitude))
                    {
                        if (!area.ContainsPosition(anchor))
                            continue;
                        if (++attempts > 120)
                            break;
                        bool started = creation.TryBeginCreation(item, anchor,
                            Quaternion.identity, null,
                            out RestaurantPlaceableObject provisional,
                            out RestaurantPlaceableCreationResult begin);
                        if (!started)
                        {
                            lastRejection = begin.Message;
                            continue;
                        }
                        if (creation.TryCommitActiveCreation(
                                out RestaurantPlaceableCreationResult commit))
                        {
                            placed = provisional;
                            instanceId = placed.InstanceId;
                            firstUnityInstanceId = placed.GetInstanceID();
                            if (SessionState.GetString(FamilyKey, "Table") == "BarCounter")
                            {
                                try { VerifyBarRuntime(placed); }
                                catch (Exception e) { Finish(false, e.Message); return; }
                            }
                            if (!editMode.TryExitEditMode(true, out _))
                            {
                                Finish(false, "Edit Mode could not close after placement.");
                                return;
                            }
                            saveGame.OperationCompleted -= OnSaveOperationCompleted;
                            saveGame.OperationCompleted += OnSaveOperationCompleted;
                            SessionState.SetString(StageKey,
                                commandLine ? "save_cli" : "save_menu");
                            deadline = EditorApplication.timeSinceStartup + 120d;
                            if (!saveGame.TrySaveSlot(slot,
                                    "SAVIC RUNTIME " + SessionState.GetString(FamilyKey, "Table") + " TEST", out string rejection))
                                Finish(false, "Runtime save was rejected: " + rejection);
                            return;
                        }
                        RestaurantPlacementValidationResult validation = commit.ValidationResult;
                        lastRejection = commit.Message + " status=" + validation.Status +
                            ", technical=" + validation.TechnicalMessage + ", user=" + validation.UserMessage +
                            ", conflict=" + (validation.ConflictingFootprint != null ? validation.ConflictingFootprint.name :
                                validation.ConflictingObstacle != null ? validation.ConflictingObstacle.name : "none");
                        string reason = validation.Status.ToString();
                        placementRejections.TryGetValue(reason, out int count);
                        placementRejections[reason] = count + 1;
                        if (count == 0) Debug.Log("[SAVIC] Candidate placement rejected at " + anchor + ": " + lastRejection);
                        if (creation.HasActiveCreation)
                            creation.TryCancelActiveCreation(out _);
                    }
                    if (attempts > 120)
                        break;
                }
                if (attempts > 120)
                    break;
            }

            Debug.Log("[SAVIC] Candidate placement rejection totals: " + string.Join("; ", placementRejections.Select(pair => pair.Key + "=" + pair.Value)));
            Finish(false, "No free floor position accepted the SAVIC placeable after " +
                attempts + " bounded attempts. Last rejection: " + lastRejection);
        }

        private static void PrepareBarRuntimeLayout()
        {
            // The shipped dining layout is already occupied. Test its canonical
            // edit lifecycle with a temporary empty furniture layout; no scene asset is saved.
            if (!Application.isBatchMode) throw new InvalidOperationException("Bar candidate layout preparation requires an isolated batch editor.");
            RestaurantPlaceableObject[] furniture = registry.RegisteredPlaceables.Where(p => p != null &&
                (p.GetComponent<RestaurantSeat>() != null || p.GetComponent<RestaurantTable>() != null))
                .OrderBy(p => p.GetComponent<RestaurantSeat>() != null ? 0 : 1).ToArray();
            foreach (RestaurantPlaceableObject existing in furniture)
                if (!lifecycle.TryDeactivateInstance(existing, out _, out var removal))
                    throw new InvalidOperationException("Temporary runtime layout could not retire furniture: " + removal.Message);
            Physics.SyncTransforms();
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>()?.RebuildSubjects();
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialPlacementAssessmentService>()?.RefreshProviderCache();
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>()?.RebuildNavigationTopology();
            Debug.Log("[SAVIC] Temporary runtime bar layout: " + furniture.Length +
                " tables/chairs retired through the canonical lifecycle. Prototype scene asset and placement rules preserved.");
        }

        private static void OnSaveOperationCompleted(BistroBuilderSaveOperationResult result)
        {
            string stage = SessionState.GetString(StageKey, string.Empty);
            if (stage.StartsWith("cleanup_", StringComparison.Ordinal))
            {
                bool deleted = result != null && result.Succeeded &&
                    saveGame != null && !saveGame.SlotExists(slot);
                if (!deleted)
                    resultMessage += " Diagnostic slot deletion failed: " +
                                     (result?.Message ?? "null result");
                else if (resultSuccess)
                    resultMessage += " Diagnostic slot deleted.";
                Exit(resultSuccess && deleted);
                return;
            }

            if (result == null || !result.Succeeded)
            {
                Finish(false, "SaveGame failed during " + stage + ": " +
                    (result?.Message ?? "null result"));
                return;
            }

            if (stage.StartsWith("save_", StringComparison.Ordinal))
            {
                SessionState.SetString(StageKey, commandLine ? "load_cli" : "load_menu");
                deadline = EditorApplication.timeSinceStartup + 120d;
                if (!saveGame.TryLoadSlot(slot, out string rejection))
                    Finish(false, "Runtime load was rejected: " + rejection);
                return;
            }

            if (stage.StartsWith("load_", StringComparison.Ordinal))
            {
                RestaurantPlaceableObject restored = registry.RegisteredPlaceables
                    .FirstOrDefault(candidate => candidate != null &&
                        candidate.InstanceId == instanceId);
                RestaurantTable table = restored != null
                    ? restored.GetComponent<RestaurantTable>() : null;
                RestaurantTableSeatingConfiguration seating = restored != null
                    ? restored.GetComponent<RestaurantTableSeatingConfiguration>() : null;
                bool storage = SessionState.GetString(FamilyKey, "Table") == "StorageFurniture";
                bool lamp = SessionState.GetString(FamilyKey, "Table") == "FloorLamp";
                bool bar = SessionState.GetString(FamilyKey, "Table") == "BarCounter";
                bool familyValid = bar ? restored != null && restored.GetComponent<BistroBuilderBarPlaceableBinding>()?.IsRuntimeRegistered == true
                    : storage || lamp
                    ? restored != null && table == null &&
                      restored.GetComponent<RestaurantPlacementFootprint>() != null &&
                      restored.GetComponentsInChildren<Collider>(true).Any(collider => !collider.isTrigger) &&
                      restored.ItemDefinition != null &&
                      restored.ItemDefinition.Category == (lamp ? RestaurantPlaceableItemCategory.Lighting : RestaurantPlaceableItemCategory.Furniture)
                    : table != null && seating != null && seating.MaximumCustomers == 2;
                if (lamp && familyValid)
                {
                    SavicFloorLampAuthoringRecord plan = JsonUtility.FromJson<SavicFloorLampAuthoringRecord>(
                        SessionState.GetString(LampPlanKey, string.Empty));
                    SavicManifest probe = new SavicManifest
                    {
                        source = new SavicSourceRecord { sourceHash = plan.sourceHash, providerMetadataHash = plan.providerMetadataHash },
                        classification = new SavicClassificationRecord { type = "FloorLamp" },
                        floorLamp = plan,
                        genericPlaceable = new SavicGenericPlaceableAuthoringRecord
                        { requiresFunctionalAdapter = true, integrationMode = SavicFloorLampFunctionAdapter.Mode }
                    };
                    familyValid = SavicPlaceableFunctionAdapters.Validate(restored.gameObject, probe, out _);
                }
                if (restored == null || !familyValid ||
                    restored.GetInstanceID() == firstUnityInstanceId ||
                    restored.ItemDefinition == null ||
                    restored.ItemDefinition.ItemId != item.ItemId)
                {
                    Finish(false, "Loaded game did not recreate the SAVIC placeable with its ItemId and family contract.");
                    return;
                }
                if (bar)
                {
                    try { VerifyBarRuntime(restored); }
                    catch (Exception e) { Finish(false, e.Message); return; }
                }

                Finish(true, "SAVIC " + SessionState.GetString(FamilyKey, "Table").ToUpperInvariant() + " RUNTIME PLAYTEST - PASS: SavicId=" +
                    SessionState.GetString(SavicIdKey, string.Empty) +
                    ", ItemId=" + item.ItemId +
                    ", canonical placement=PASS, SaveGame save/load=PASS, " +
                    "new runtime instance=PASS, " + (bar ? "native bar binding/identity/route/lease=PASS." : lamp ? "lighting footprint/collider/category/emitter=PASS."
                        : storage ? "furniture footprint/collider/category=PASS." : "seats=2."));
            }
        }

        private static void Finish(bool success, string message)
        {
            resultSuccess = success;
            resultMessage = message;
            if (saveGame != null)
                saveGame.OperationCompleted -= OnSaveOperationCompleted;
            SessionState.SetString(StageKey, commandLine ? "cleanup_cli" : "cleanup_menu");
        }

        private static void CleanSlotOrExit()
        {
            if (slot >= 0 && saveGame != null && saveGame.SlotExists(slot))
            {
                saveGame.OperationCompleted -= OnSaveOperationCompleted;
                saveGame.OperationCompleted += OnSaveOperationCompleted;
                deadline = EditorApplication.timeSinceStartup + 30d;
                if (saveGame.TryDeleteSlot(slot, out string rejection))
                    return;
                resultMessage += " Diagnostic slot deletion rejected: " + rejection;
                Exit(false);
                return;
            }
            Exit(resultSuccess);
        }

        private static void Exit(bool success)
        {
            if (saveGame != null)
                saveGame.OperationCompleted -= OnSaveOperationCompleted;
            string reportPath = SessionState.GetString(ReportPathKey, string.Empty);
            string report = (success ? "PASS: " : "FAIL: ") + resultMessage;
            if (!string.IsNullOrWhiteSpace(reportPath))
                File.WriteAllText(reportPath, report);
            SessionState.SetBool(SuccessKey, success);
            SessionState.SetString(StageKey, commandLine ? "exit_cli" : "exit_menu");
            EditorApplication.ExitPlaymode();
        }

        private static void StampBarAcceptance(string reportPath)
        {
                    SavicEditorContext context = SavicEditorContext.Instance;
                    if (!context.Manifests.TryGetBySavicId(SessionState.GetString(SavicIdKey, ""), out SavicManifest manifest))
                        throw new InvalidOperationException("Bar acceptance manifest disappeared.");
                    SavicManifest expected = JsonUtility.FromJson<SavicManifest>(SessionState.GetString(BarManifestKey, ""));
                    if (manifest.barCounter.inputFingerprint != expected.barCounter.inputFingerprint)
                        throw new InvalidOperationException("Bar authoring changed during runtime acceptance.");
                    if (!SavicPlaceableFunctionAdapters.Validate(AssetDatabase.LoadAssetAtPath<GameObject>(
                        manifest.genericPlaceable.prefabAssetPath), manifest, out string authoringError))
                        throw new InvalidOperationException("Bar function authoring is not current: " + authoringError);
                    manifest.barCounterRuntime = new SavicBarCounterRuntimeAcceptanceRecord {
                        verifierVersion = SavicBarCounterRuntimeAcceptance.Version,
                        planFingerprint = manifest.barCounter.inputFingerprint, sourceHash = manifest.source.sourceHash,
                        prefabDependencyHash = AssetDatabase.GetAssetDependencyHash(manifest.genericPlaceable.prefabAssetPath).ToString(),
                        reportRelativePath = context.Layout.ToProjectRelativePath(reportPath), reportHash = SavicHashService.ComputeSha256(reportPath),
                        verifiedUtc = DateTime.UtcNow.ToString("O"), creationPassed = true, nativeBindingPassed = true,
                        routePassed = true, leasePassed = true, saveLoadPassed = true, cleanupPassed = true, consoleClean = true };
                    context.Manifests.Save(manifest);
        }

        private static void ConfigureCandidateCatalog()
        {
            RestaurantPlaceableCatalogService catalog = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>();
            BistroBuilderSaveDefinitionCatalog definitions = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveDefinitionCatalog>();
            if (catalog?.CatalogDefinition == null || definitions == null)
                throw new InvalidOperationException("Canonical runtime catalog authorities are absent.");
            runtimeCatalog = UnityEngine.Object.Instantiate(catalog.CatalogDefinition);
            runtimeCatalog.name = "SAVIC Candidate Runtime Catalog";
            SerializedObject copy = new SerializedObject(runtimeCatalog);
            SerializedProperty items = copy.FindProperty("items");
            items.arraySize++; items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = item;
            copy.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject service = new SerializedObject(catalog);
            service.FindProperty("catalogDefinition").objectReferenceValue = runtimeCatalog; service.ApplyModifiedPropertiesWithoutUndo();
            catalog.RebuildCatalog();
            SerializedObject saveDefinitions = new SerializedObject(definitions);
            SerializedProperty sources = saveDefinitions.FindProperty("sourceCatalogs");
            sources.arraySize++; sources.GetArrayElementAtIndex(sources.arraySize - 1).objectReferenceValue = runtimeCatalog;
            saveDefinitions.ApplyModifiedPropertiesWithoutUndo(); definitions.RebuildIndex();
            if (!catalog.TryGetItem(item.ItemId, out var resolved) || resolved != item ||
                !definitions.TryGetDefinition(item.ItemId, out var saved) || saved != item)
                throw new InvalidOperationException("Canonical runtime catalog/save resolver cannot resolve the candidate.");
        }

        private static void VerifyBarRuntime(RestaurantPlaceableObject target)
        {
            SavicManifest manifest = JsonUtility.FromJson<SavicManifest>(SessionState.GetString(BarManifestKey, ""));
            if (!SavicPlaceableFunctionAdapters.Validate(target.gameObject, manifest, out string error))
                throw new InvalidOperationException("Runtime bar differs from plan: " + error);
            BistroBuilderBarPlaceableBinding binding = target.GetComponent<BistroBuilderBarPlaceableBinding>();
            BistroBuilderBarServiceRegistry bars = UnityEngine.Object.FindFirstObjectByType<BistroBuilderBarServiceRegistry>();
            BistroBuilderSpatialInteractionService spatial = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
            BistroBuilderNavigationService navigation = UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
            BistroBuilderOperationalSpatialCoordinator coordinator = UnityEngine.Object.FindFirstObjectByType<BistroBuilderOperationalSpatialCoordinator>();
            if (bars == null || spatial == null || navigation == null || coordinator == null || !binding.IsRuntimeRegistered)
                throw new InvalidOperationException("Runtime bar/native authority registration is incomplete.");
            BistroBuilderBarServiceSpot spot = binding.Spots[0];
            string spotId = BistroBuilderBarPlaceableBinding.BuildSpotId(target.InstanceId, 0);
            if (spot.BarSpotId != spotId || !bars.TryGetSpot(spotId, out var found) || found != spot ||
                !spatial.TryGetSubject(BistroBuilderBarBodySpatialAdapter.BuildBodyId(target.InstanceId), out var body) || body.gameObject != target.gameObject)
                throw new InvalidOperationException("Runtime bar/body identities are not derived from the persisted placeable instance.");
            GameObject groupNode = new GameObject("SAVIC Bar Acceptance Customer");
            CustomerGroup group = groupNode.AddComponent<CustomerGroup>();
            BistroBuilderBarSpatialAdapter adapter = spot.GetComponent<BistroBuilderBarSpatialAdapter>();
            try
            {
                groupNode.transform.position = spot.CustomerPoint.position;
                if (!group.Initialize(99123, 1, BistroBuilderServiceMode.WaitingAtBar) ||
                    !bars.TryAllocateSpot(group, BistroBuilderServiceMode.WaitingAtBar, out var allocated) || allocated != spot)
                    throw new InvalidOperationException("Runtime bar allocation failed.");
                coordinator.ReconcileOperationalClaims();
                if (!adapter.HasCustomerLease || binding.CanDeactivate(out _))
                    throw new InvalidOperationException("Runtime bar allocation, customer lease or occupied guard failed: " + error);
                navigation.RebuildNavigationTopology();
                float extent = Math.Abs(manifest.barCounter.openingDirection.x) > 0.5f ? manifest.barCounter.finalSizeMeters.x * 0.5f
                    : manifest.barCounter.finalSizeMeters.z * 0.5f;
                Vector3 origin = target.transform.TransformPoint(manifest.barCounter.waiterLocalPosition +
                    manifest.barCounter.openingDirection * (extent + 2));
                List<Vector3> points = new List<Vector3>();
                if (!navigation.TryBuildRoute("savic.bar.acceptance.waiter", BistroBuilderNavigationAgentMask.Waiter, origin,
                    spot.WaiterServicePoint.position, 0.28f, points, out _, out var kind) || kind == BistroBuilderNavigationRouteKind.DirectDegraded)
                    throw new InvalidOperationException("Runtime waiter cannot reach the bar service point.");
                List<RestaurantPlacementShape> shapes = new List<RestaurantPlacementShape>();
                if (!BistroBuilderPhysicalPlacementGeometry.TryWriteShapes(target.GetComponent<RestaurantPlacementFootprint>(),
                    target.transform.position, target.transform.rotation, shapes, out error)) throw new InvalidOperationException(error);
                List<RestaurantPlacementShape> agent = new List<RestaurantPlacementShape>();
                Vector3 previous = origin;
                foreach (Vector3 next in points)
                {
                    int count = Math.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, next) / 0.05f));
                    for (int n = 0; n <= count; n++)
                    {
                        agent.Clear(); agent.Add(new RestaurantPlacementShape(Vector3.Lerp(previous, next, n / (float)count),
                            Vector3.right, Vector3.forward, Vector2.one * 0.28f, 0));
                        if (BistroBuilderPhysicalPlacementGeometry.EvaluateConflict(shapes, agent) != RestaurantPlacementConflictType.None)
                            throw new InvalidOperationException("Runtime waiter route crosses the authored bar body.");
                    }
                    previous = next;
                }
                if (!bars.ReleaseGroup(group)) throw new InvalidOperationException("Runtime native bar release failed.");
                coordinator.ReconcileOperationalClaims();
                if (adapter.HasCustomerLease) throw new InvalidOperationException("Runtime operational coordinator did not release the native customer's lease.");
                Debug.Log("[SAVIC] BAR CANDIDATE RUNTIME NATIVE CONTRACT - PASS: persisted identities, allocation/automatic coordinator lease/release/busy guard and real waiter route=" + kind + ".");
            }
            finally { bars.ReleaseGroup(group); adapter.ReleaseCustomerLease(); UnityEngine.Object.Destroy(groupNode); }
        }
    }
}
