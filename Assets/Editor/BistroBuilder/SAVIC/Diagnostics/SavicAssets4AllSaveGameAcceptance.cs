using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [InitializeOnLoad]
    public static class SavicAssets4AllSaveGameAcceptance
    {
        private const string Key = "SAVIC.Assets4ALL.CrossRevision.";
        private const string Id = "8a5c37cab8eb4366ab675afa66af65ad";
        private static BistroBuilderSaveGameService saves;
        private static RestaurantPlaceableRegistry registry;
        private static double deadline;
        private static int screenshotFrame;
        static SavicAssets4AllSaveGameAcceptance()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }
        public static void RunSaveFromCommandLine() => Begin("save");
        public static void RunLoadFromCommandLine() => Begin("load");
        private static string Folder => Path.Combine(SavicStorageLayout.ForCurrentProject().SavicRoot, "Verification", "Assets4All");
        private static string CheckpointPath => Path.Combine(Folder, "cross-revision-checkpoint.json");
        private static void Begin(string phase)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor for this acceptance.");
            SessionState.SetString(Key + "phase", phase); SessionState.SetString(Key + "stage", "enter");
            SessionState.SetInt(Key + "errors", 0); SessionState.SetBool(Key + "success", false);
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static void Log(string message, string stack, LogType type)
        {
            if (SessionState.GetString(Key + "stage", "") == "") return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { SessionState.SetInt(Key + "errors", SessionState.GetInt(Key + "errors", 0) + 1); SessionState.SetString(Key + "lastError", message); }
        }
        private static void ModeChanged(PlayModeStateChange mode)
        {
            if (SessionState.GetString(Key + "stage", "") == "") return;
            if (mode == PlayModeStateChange.EnteredPlayMode)
            { deadline = EditorApplication.timeSinceStartup + 180; SessionState.SetString(Key + "stage", "init"); }
            else if (mode == PlayModeStateChange.EnteredEditMode)
            {
                bool pass = SessionState.GetBool(Key + "success", false) && SessionState.GetInt(Key + "errors", 0) == 0;
                string phase = SessionState.GetString(Key + "phase", ""); Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "savegame-" + phase + "-result.txt"), (pass ? "PASS: " : "FAIL: ") +
                    SessionState.GetString(Key + "message", "") + " Console errors=" + SessionState.GetInt(Key + "errors", 0) + "; " + SessionState.GetString(Key + "lastError", ""));
                SessionState.EraseString(Key + "stage"); EditorApplication.Exit(pass ? 0 : 1);
            }
        }
        private static void Tick()
        {
            string stage = SessionState.GetString(Key + "stage", "");
            if (!EditorApplication.isPlaying || stage == "" || stage == "exit") return;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Runtime timeout during " + stage); return; }
            if (stage == "capture" && Time.frameCount >= screenshotFrame)
            {
                if (SessionState.GetString(Key + "phase", "") == "load")
                {
                    var checkpoint = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(CheckpointPath));
                    SessionState.SetString(Key + "stage", "delete");
                    if (!saves.TryDeleteSlot(checkpoint.slot, out string error)) Finish(false, error);
                }
                else Finish(true, "Revision 1 placed through native Edit Mode and saved in the universal SaveGame service. Checkpoint retained for revision 2.");
                return;
            }
            if (stage != "init" || Time.frameCount < 3) return;
            try
            {
                saves = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
                registry = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableRegistry>();
                var creation = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCreationService>();
                var edit = UnityEngine.Object.FindFirstObjectByType<RestaurantEditModeService>();
                var catalog = UnityEngine.Object.FindFirstObjectByType<RestaurantPlaceableCatalogService>();
                var definitions = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveDefinitionCatalog>();
                if (saves == null || registry == null || creation == null || edit == null || registry.RegisteredPlaceableCount == 0) return;
                if (!SavicEditorContext.Instance.Manifests.TryGetBySavicId(Id, out var manifest) || manifest.status != "PUBLISHED")
                    throw new InvalidOperationException("Published Assets4ALL cabinet is missing.");
                var item = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(manifest.genericPlaceable.itemDefinitionAssetPath);
                if (catalog == null || definitions == null || !catalog.TryGetItem(item.ItemId, out var current) || current != item ||
                    !definitions.TryGetDefinition(item.ItemId, out var saved) || saved != item)
                    throw new InvalidOperationException("Actual main catalog and SaveGame do not resolve the revised article.");
                saves.OperationCompleted -= Saved; saves.OperationCompleted += Saved;
                if (SessionState.GetString(Key + "phase", "") == "load")
                {
                    var checkpoint = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(CheckpointPath));
                    if (checkpoint.itemId != item.ItemId || manifest.assets4All.delivery.revision < 2) throw new InvalidOperationException("Cross-revision identity mismatch.");
                    SessionState.SetString(Key + "stage", "load");
                    if (!saves.TryLoadSlot(checkpoint.slot, out string error)) Finish(false, error);
                    return;
                }
                if (manifest.assets4All.delivery.revision != 1) throw new InvalidOperationException("Save checkpoint must use revision 1.");
                int slot = Enumerable.Range(960, 20).First(s => !saves.SlotExists(s));
                if (!edit.IsEditModeActive && !edit.TryEnterEditMode(out _, out string rejection)) throw new InvalidOperationException(rejection);
                var reference = registry.RegisteredPlaceables.First(p => p != null && p.GetComponent<RestaurantTable>() != null && p.PlacementAnchor != null);
                float y = reference.PlacementAnchor.position.y;
                int attempts = 0;
                foreach (var area in UnityEngine.Object.FindObjectsByType<RestaurantArea>(FindObjectsSortMode.None))
                foreach (var boundary in area.BoundaryColliders.Where(c => c != null && c.enabled))
                {
                    Bounds bounds = boundary.bounds;
                    for (float x = bounds.min.x + .55f; x <= bounds.max.x - .55f; x += .85f)
                    for (float z = bounds.min.z + .55f; z <= bounds.max.z - .55f; z += .85f)
                    {
                        Vector3 anchor = new Vector3(x, y, z); if (!area.ContainsPosition(anchor)) continue;
                        if (++attempts > 300) throw new InvalidOperationException("No valid native placement after 300 bounded attempts.");
                        if (!creation.TryBeginCreation(item, anchor, Quaternion.identity, null, out var placed, out _)) continue;
                        if (!creation.TryCommitActiveCreation(out _)) { if (creation.HasActiveCreation) creation.TryCancelActiveCreation(out _); continue; }
                        if (!edit.TryExitEditMode(true, out _)) throw new InvalidOperationException("Could not exit Edit Mode.");
                        Directory.CreateDirectory(Folder);
                        File.WriteAllText(CheckpointPath, JsonUtility.ToJson(new Checkpoint { slot = slot, instanceId = placed.InstanceId, itemId = item.ItemId,
                            sourceHash = manifest.source.sourceHash, firstUnityInstance = placed.GetInstanceID() }, true));
                        SessionState.SetString(Key + "stage", "save");
                        if (!saves.TrySaveSlot(slot, "Assets4ALL cross revision acceptance", out string error)) Finish(false, error);
                        return;
                    }
                }
                throw new InvalidOperationException("No native placement candidate succeeded.");
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        private static void Saved(BistroBuilderSaveOperationResult outcome)
        {
            try
            {
                if (outcome == null || !outcome.Succeeded) { Finish(false, outcome?.Message ?? "Null SaveGame outcome"); return; }
                string stage = SessionState.GetString(Key + "stage", "");
                if (stage == "delete") { Finish(true, "Prior revision save loaded with the current model, stable instance/item IDs, manual finish and main catalog. Diagnostic slot deleted."); return; }
                if (stage == "load")
                {
                    var checkpoint = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(CheckpointPath));
                    var restored = registry.RegisteredPlaceables.Single(p => p != null && p.InstanceId == checkpoint.instanceId);
                    var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/BistroBuilder/SAVIC/AcceptanceProtectedFinish.mat");
                    if (restored.ItemDefinition?.ItemId != checkpoint.itemId ||
                        !restored.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Contains(material)))
                        throw new InvalidOperationException("Saved identity or protected finish lost when loading revised model.");
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(Folder, "runtime-" + SessionState.GetString(Key + "phase", "") + ".png"));
                screenshotFrame = Time.frameCount + 10; SessionState.SetString(Key + "stage", "capture");
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        private static void Finish(bool pass, string message)
        {
            if (saves != null) saves.OperationCompleted -= Saved;
            SessionState.SetBool(Key + "success", pass); SessionState.SetString(Key + "message", message);
            SessionState.SetString(Key + "stage", "exit"); EditorApplication.ExitPlaymode();
        }
        [Serializable] private class Checkpoint { public int slot, firstUnityInstance; public string instanceId, itemId, sourceHash; }
    }
}
