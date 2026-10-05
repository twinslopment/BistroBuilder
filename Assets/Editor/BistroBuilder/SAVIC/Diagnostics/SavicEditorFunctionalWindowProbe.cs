using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BistroBuilder.Editor.Savic
{
    // Activates the real UI button; the existing acceptance runners retain all
    // source, native runtime, SaveGame, cleanup and Console assertions.
    [InitializeOnLoad]
    public static class SavicEditorFunctionalWindowProbe
    {
        private const string Key = "SAVIC.FunctionalWindowProbe.";
        private const string Scene = "Assets/Generated/BistroBuilder/SAVIC/Diagnostics/EditorFunctionalWindowProbe.unity";
        [Serializable] private sealed class Fixture
        {
            public string id, type, manifestPath, manifest, reportPath, report, queue;
            public bool queueExisted, revision, acceptCurrent; public int stage;
            public string input, proposedHash, itemGuid, prefabGuid;
            public double deadline;
        }
        static SavicEditorFunctionalWindowProbe() { EditorApplication.update += Tick; }
        public static void RunBarFromCommandLine() => Begin("BarCounter");
        public static void RunStoolFromCommandLine() => Begin("BarStool");
        public static void RunHoodFromCommandLine() => Begin("KitchenEquipment");
        public static void RunBarRevisionFromCommandLine() => Begin("BarCounter", true);
        public static void RenewStaleStoolFromCommandLine() => Begin("BarStool", false, true);
        private static void Begin(string type, bool revision = false, bool acceptCurrent = false)
        {
            var c = SavicEditorContext.Instance;
            Require(!SessionState.GetBool(Key + "Active", false) && !SavicRuntimeVerificationSession.IsActive && c.Jobs.PendingProcessCount == 0, "An operation is already active.");
            var m = c.Manifests.GetAll().First(m => m.status == "PUBLISHED" && m.type == type && SavicFunctionalRuntimeAcceptance.Required(m) && (!acceptCurrent || !SavicFunctionalRuntimeAcceptance.Matches(m, c.Layout)));
            Require(acceptCurrent || SavicFunctionalRuntimeAcceptance.Matches(m, c.Layout), "Published runtime proof must be current before strict UI revalidation.");
            c.Manifests.TryGetManifestPath(m.savicId, out string manifestPath);
            string report = m.barCounterRuntime?.reportRelativePath;
            if (type == "BarStool") report = m.barStoolRuntime.reportRelativePath;
            if (type == "KitchenEquipment") report = m.overheadEquipmentRuntime.reportRelativePath;
            var f = new Fixture { id = m.savicId, type = type, manifestPath = manifestPath, manifest = Convert.ToBase64String(File.ReadAllBytes(manifestPath)),
                reportPath = c.Layout.FromProjectRelativePath(report), report = Convert.ToBase64String(File.ReadAllBytes(c.Layout.FromProjectRelativePath(report))),
                queueExisted = File.Exists(c.Layout.QueueSnapshotPath), queue = File.Exists(c.Layout.QueueSnapshotPath) ? File.ReadAllText(c.Layout.QueueSnapshotPath) : "",
                deadline = EditorApplication.timeSinceStartup + 300 };
            f.acceptCurrent = acceptCurrent; f.revision = revision; f.itemGuid = AssetDatabase.AssetPathToGUID(m.genericPlaceable.itemDefinitionAssetPath); f.prefabGuid = AssetDatabase.AssetPathToGUID(m.genericPlaceable.prefabAssetPath);
            if (revision) Require(SavicSourceUpdateService.ReadLast(c.Layout, m.savicId) == null, "Revision probe requires no existing update journal.");
            Require(!File.Exists(c.Layout.FromProjectRelativePath(Scene)), "Diagnostic scene must not overwrite an existing file.");
            Directory.CreateDirectory(Path.GetDirectoryName(c.Layout.FromProjectRelativePath(Scene))); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Require(EditorSceneManager.SaveScene(scene, Scene), "Diagnostic scene not saved.");
            c.Jobs.SetPaused(true);
            SessionState.SetString(Key + "Fixture", JsonUtility.ToJson(f));
            SessionState.SetBool(Key + "Active", true); SessionState.SetInt(Key + "Errors", 0);
            var window = ScriptableObject.CreateInstance<SavicEditorWindow>(); window.Show(); window.CreateGUI(); window.FlushRefreshForDiagnostics();
            window.ShowAssetForDiagnostics(m.savicId);
            if (revision)
            {
                f.input = Path.Combine(c.Layout.LogsRoot, "FunctionalRevisionInput", Guid.NewGuid().ToString("N"), m.source.originalFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(f.input));
                SavicEditorWorkflowProbe.WriteVariant(c.Layout.FromProjectRelativePath(m.source.archivedRelativePath), f.input, Guid.NewGuid().ToString("N"));
                f.proposedHash = SavicHashService.ComputeSha256(f.input);
                SessionState.SetString(Key + "Fixture", JsonUtility.ToJson(f));
                SavicEditorWindow.SourceRevisionSelection = () => f.input;
            }
            var button = window.rootVisualElement.Q<Button>(revision ? "savic-update-source" : "savic-verify");
            Require(button != null && button.enabledSelf, "Native functional verification button unavailable.");
            button.Focus(); using var submit = NavigationSubmitEvent.GetPooled(); submit.target = button; button.SendEvent(submit);
            Require(SavicRuntimeVerificationSession.IsActive && SavicRuntimeVerificationSession.SelectedId == m.savicId,
                "Native button failed to start the exact selected verification.");
            Debug.Log("[SAVIC] Functional native window action started for exact selected " + type + " " + m.savicId);
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "Active", false)) return;
            var f = JsonUtility.FromJson<Fixture>(SessionState.GetString(Key + "Fixture", ""));
            if (SavicRuntimeVerificationSession.IsActive || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.timeSinceStartup <= f.deadline) return;
                Debug.LogError("[SAVIC] Functional window probe timed out. Runner checkpoint retained for safe recovery.");
                SessionState.SetBool(Key + "Active", false); EditorApplication.Exit(1); return;
            }
            SessionState.SetBool(Key + "Active", false);
            int exit = 0;
            try
            {
                var c = SavicEditorContext.Instance;
                Require(SavicRuntimeVerificationSession.LastSucceeded, SavicRuntimeVerificationSession.LastResult);
                Require(SceneManager.GetActiveScene().path == Scene && c.Jobs.IsPaused, "Verification did not restore the saved scene and prior paused state.");
                Require(c.Manifests.TryGetBySavicId(f.id, out var m) && m.status == "PUBLISHED" && SavicFunctionalRuntimeAcceptance.Matches(m, c.Layout),
                    "Selected UI verification changed publication or failed current proof matching.");
                if (f.revision)
                {
                    Require(m.source.sourceHash == f.proposedHash && m.sourceRevisions.Any(r => r.sourceHash != f.proposedHash) &&
                        SavicSourceUpdateService.ReadLast(c.Layout, f.id).state == "COMMITTED" &&
                        AssetDatabase.AssetPathToGUID(m.genericPlaceable.itemDefinitionAssetPath) == f.itemGuid &&
                        AssetDatabase.AssetPathToGUID(m.genericPlaceable.prefabAssetPath) == f.prefabGuid && File.Exists(f.input),
                        "Functional update did not commit the accepted source with stable GUIDs and retained external original.");
                    if (f.stage == 0)
                    {
                        File.Copy(f.reportPath, Path.Combine(c.Layout.LogsRoot, "editor-functional-revision-candidate-runtime.txt"), true);
                        f.stage = 1; f.deadline = EditorApplication.timeSinceStartup + 300;
                        SessionState.SetString(Key + "Fixture", JsonUtility.ToJson(f)); SessionState.SetBool(Key + "Active", true);
                        var window = ScriptableObject.CreateInstance<SavicEditorWindow>(); window.Show(); window.CreateGUI(); window.FlushRefreshForDiagnostics(); window.ShowAssetForDiagnostics(f.id);
                        var verify = window.rootVisualElement.Q<Button>("savic-verify"); Require(verify != null && verify.enabledSelf, "Published revision verification button unavailable.");
                        verify.Focus(); using var submit = NavigationSubmitEvent.GetPooled(); submit.target = verify; verify.SendEvent(submit);
                        Require(SavicRuntimeVerificationSession.IsActive, "Strict published revision verification did not start."); return;
                    }
                    Require(!SessionState.GetBool("SAVIC.RuntimeTable.BarCandidate", true), "Final revised proof did not use actual MainCatalog.");
                }
                File.Copy(f.reportPath, Path.Combine(c.Layout.LogsRoot, "editor-functional-window-" + f.type + "-runtime.txt"), true);
                File.WriteAllText(Path.Combine(c.Layout.LogsRoot, "editor-functional-window-" + f.type + "-proof.json"), JsonUtility.ToJson(m, true));
                File.WriteAllText(Path.Combine(c.Layout.LogsRoot, "editor-functional-window-" + f.type + "-result.txt"), DateTime.UtcNow.ToString("O") +
                    "\nPASS: exact selected native verification button; strict main catalog native runtime assertions, current source/proof, actual SaveGame and cleanup; saved Editor scene and prior queue pause restored.\n" + SavicRuntimeVerificationSession.LastResult);
                Debug.Log("[SAVIC] FUNCTIONAL EDITOR WINDOW - PASS: " + (f.revision ? "source revision candidate then MainCatalog; " : "") + f.type + "; exact selected asset, real runtime acceptance, saved scene and paused queue restored.");
            }
            catch (Exception error) { exit = 1; Debug.LogError("[SAVIC] FUNCTIONAL EDITOR WINDOW - FAIL: " + error); }
            finally
            {
                if (!SessionState.GetBool(Key + "Active", false))
                {
                foreach (var window in Resources.FindObjectsOfTypeAll<SavicEditorWindow>()) window.Close();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); AssetDatabase.DeleteAsset(Scene);
                if (f.revision)
                {
                    var context = SavicEditorContext.Instance;
                    new SavicSourceUpdateService(context).RestoreLastForDiagnostics(f.id);
                    string request = Path.Combine(context.Layout.SavicRoot, "SourceUpdates", f.id + ".json"); if (File.Exists(request)) File.Delete(request);
                    string mirror = Path.GetDirectoryName(context.Layout.GetUnitySourceMirrorPath(f.proposedHash, Path.GetFileName(f.input)));
                    AssetDatabase.DeleteAsset(context.Layout.ToProjectRelativePath(mirror));
                    string archive = Path.GetDirectoryName(context.Layout.GetArchivedSourcePath(f.proposedHash, Path.GetFileName(f.input)));
                    Require(Path.GetFullPath(archive).StartsWith(Path.GetFullPath(context.Layout.ContentSourceRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Fixture archive escaped canonical root.");
                    if (Directory.Exists(archive)) Directory.Delete(archive, true);
                }
                SavicEditorWindow.SourceRevisionSelection = null;
                if (!f.acceptCurrent) { File.WriteAllBytes(f.manifestPath, Convert.FromBase64String(f.manifest)); File.WriteAllBytes(f.reportPath, Convert.FromBase64String(f.report)); }
                var c = SavicEditorContext.Instance;
                if (f.queueExisted) File.WriteAllText(c.Layout.QueueSnapshotPath, f.queue); else if (File.Exists(c.Layout.QueueSnapshotPath)) File.Delete(c.Layout.QueueSnapshotPath);
                c.Manifests.Reload(); c.Jobs.Reload();
                Require(c.Manifests.TryGetBySavicId(f.id, out var original) && SavicFunctionalRuntimeAcceptance.Matches(original, c.Layout), "Diagnostic fixture did not restore current baseline proof.");
                SessionState.EraseString(Key + "Fixture");
                }
            }
            EditorApplication.Exit(exit);
        }
        private static void Require(bool valid, string error) { if (!valid) throw new InvalidOperationException(error); }
    }
}