using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace BistroBuilder.Editor.Savic
{
    // Two actual Editor launches: first imports through the UI and exits with
    // a claimed job; second recovers, resumes, publishes and updates that asset.
    public static class SavicEditorWorkflowProbe
    {
        private const string Catalog = "Assets/Data/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalog_Main.asset";
        private const string Scene = "Assets/Generated/BistroBuilder/SAVIC/Diagnostics/EditorWorkflowProbe.unity";
        [Serializable] private sealed class Fixture
        {
            public string id, hash, sourceName, originalHash, originalArchive, queue, catalog, catalogMeta, input, jobId;
            public int count; public bool queueExisted;
            public string recoveryHash, recoveryPrefabHash, itemPath, prefabPath, itemGuid, prefabGuid;
            public List<string> hashes = new List<string>();
        }
        private static string State => Path.Combine(SavicEditorContext.Instance.Layout.LogsRoot, "editor-workflow-state.json");
        private static int checks;
        private static SavicEditorWindow window;
        public static void BeginColdRestartFromCommandLine()
        {
            var c = SavicEditorContext.Instance;
            Require(c.Jobs.PendingProcessCount == 0 && !c.Intake.IsBusy, "Existing queue must be idle.");
            var original = c.Manifests.GetAll().First(m => m.status == "PUBLISHED" && m.type == "Chair" &&
                m.source.extension == ".glb" && File.Exists(c.Layout.FromProjectRelativePath(m.source.archivedRelativePath)));
            var f = new Fixture { sourceName = original.source.originalFileName, originalHash = original.source.sourceHash,
                originalArchive = original.source.archivedRelativePath, count = c.Manifests.Count,
                queueExisted = File.Exists(c.Layout.QueueSnapshotPath), queue = File.Exists(c.Layout.QueueSnapshotPath) ? File.ReadAllText(c.Layout.QueueSnapshotPath) : "",
                catalog = Convert.ToBase64String(File.ReadAllBytes(c.Layout.FromProjectRelativePath(Catalog))),
                catalogMeta = Convert.ToBase64String(File.ReadAllBytes(c.Layout.FromProjectRelativePath(Catalog + ".meta"))),
                input = Path.Combine(c.Layout.LogsRoot, "EditorWorkflowInput", Guid.NewGuid().ToString("N"), original.source.originalFileName) };
            try
            {
            Directory.CreateDirectory(Path.GetDirectoryName(f.input));
            WriteVariant(c.Layout.FromProjectRelativePath(original.source.archivedRelativePath), f.input, Guid.NewGuid().ToString("N"));
            f.hash = SavicHashService.ComputeSha256(f.input); f.hashes.Add(f.hash);
            SaveScene();
            c.Jobs.SetPaused(true);
            window = ScriptableObject.CreateInstance<SavicEditorWindow>(); window.Show(); window.CreateGUI(); window.FlushRefreshForDiagnostics();
            SavicEditorWindow.ImportFolderSelection = () => Path.GetDirectoryName(f.input);
            ClickText("Importar carpeta GLB");
            Require(c.Manifests.TryGetBySourceHash(f.hash, out var m), "Native import button did not ingest the new source.");
            f.id = m.savicId;
            Require(c.Manifests.Count == f.count + 1 && File.Exists(f.input), "Import duplicated data or deleted the external original.");
            c.Jobs.SetPaused(false);
            Require(c.Jobs.TryClaimNextProcessable(out var job) && job.manifestSavicId == f.id, "Native imported job was not claimable.");
            f.jobId = job.jobId;
            c.Jobs.SetPaused(true);
            Require(job.state == SavicJobState.Processing.ToString(), "Interrupted checkpoint was not actually processing.");
            SavicAtomicFile.WriteJson(State, f);
            window.Close(); SavicEditorWindow.ImportFolderSelection = null;
            Debug.Log("[SAVIC] EDITOR WORKFLOW COLD CHECKPOINT - PASS: UI imported new real GLB, external original retained; claimed job persisted before actual Editor exit.");
            EditorApplication.Exit(0);
            } catch (Exception failure) { Debug.LogError("[SAVIC] Workflow checkpoint failed: " + failure); Cleanup(f); throw; }
        }

        public static void CompleteAfterColdRestartFromCommandLine() => CompleteAfterColdRestart(false);
        public static void PrepareRevisionColdRollbackFromCommandLine() => CompleteAfterColdRestart(true);
        private static void CompleteAfterColdRestart(bool prepareRollback)
        {
            var c = SavicEditorContext.Instance;
            var f = SavicAtomicFile.ReadJson<Fixture>(State);
            Require(f != null, "Cold checkpoint missing.");
            bool keep = false;
            try
            {
                c.Jobs.RecoverInterruptedJobs();
                Require(c.Jobs.Jobs.Single(j => j.jobId == f.jobId).state == SavicJobState.Ingested.ToString(),
                    "Interrupted job was not recovered to the canonical queue.");
                EditorSceneManager.OpenScene(Scene);
                window = ScriptableObject.CreateInstance<SavicEditorWindow>(); window.Show(); window.CreateGUI(); window.FlushRefreshForDiagnostics();
                ClickText("Cola"); ClickText("Reanudar cola");
                Require(!c.Jobs.IsPaused, "Native resume button did not resume the queue.");
                Drain(c);
                Require(c.Manifests.TryGetBySavicId(f.id, out var m) && m.status == "PUBLISHED", "New source did not publish after cold recovery.");
                string itemPath = m.chairAuthoring.itemDefinitionAssetPath, prefabPath = m.chairAuthoring.prefabAssetPath;
                var item = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(itemPath);
                string itemGuid = AssetDatabase.AssetPathToGUID(itemPath), prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                var manual = new SerializedObject(item); manual.FindProperty("purchasePrice").intValue = 99917;
                manual.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(item); AssetDatabase.SaveAssets();
                window.ShowAssetForDiagnostics(f.id);
                var retry = window.rootVisualElement.Q<Button>("savic-reprocess");
                Require(retry != null && retry.enabledSelf, "Published asset lacks an operative revalidation button.");
                Submit(retry);
                Require(c.Jobs.PendingProcessCount == 1, "Native retry button bypassed or failed to enqueue.");
                Drain(c);
                Require(c.Manifests.TryGetBySavicId(f.id, out m) && m.status == "PUBLISHED", "Native revalidation did not return to published.");
                WriteVariant(f.input, f.input, Guid.NewGuid().ToString("N"));
                string revisedHash = SavicHashService.ComputeSha256(f.input); f.hashes.Add(revisedHash);
                SavicAtomicFile.WriteJson(State, f);
                SavicEditorWindow.SourceRevisionSelection = () => f.input;
                window.ShowAssetForDiagnostics(f.id);
                var update = window.rootVisualElement.Q<Button>("savic-update-source");
                Require(update != null && update.enabledSelf, "Source update action missing.");
                Require(EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene), "Could not save fixture before update.");
                Submit(update);
                Require(c.Manifests.TryGetBySavicId(f.id, out m) && m.status == "PUBLISHED" && m.source.sourceHash == revisedHash,
                    "Native source update did not publish the current revision.");
                Require(c.Manifests.Count == f.count + 1 && m.sourceRevisions.Count == 1 &&
                    m.sourceRevisions[0].sourceHash == f.hash, "Source update duplicated identity or lost original history.");
                Require(AssetDatabase.AssetPathToGUID(itemPath) == itemGuid && AssetDatabase.AssetPathToGUID(prefabPath) == prefabGuid,
                    "Source update changed published GUIDs.");
                item = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(itemPath);
                Require(new SerializedObject(item).FindProperty("purchasePrice").intValue == 99917, "Manual purchase price was overwritten.");
                var catalog = new SerializedObject(AssetDatabase.LoadAssetAtPath<RestaurantPlaceableCatalogDefinition>(Catalog));
                var items = catalog.FindProperty("items"); int matches = 0;
                for (int i = 0; i < items.arraySize; i++)
                    if (items.GetArrayElementAtIndex(i).objectReferenceValue == item) matches++;
                Require(matches == 1, "Updated asset has duplicate catalog entries.");
                Require(SavicSourceUpdateService.ReadLast(c.Layout, f.id).state == "COMMITTED", "Update transaction did not commit.");

                File.Copy(c.Layout.FromProjectRelativePath(m.sourceRevisions[0].archivedRelativePath), f.input, true);
                window.CreateGUI(); SavicEditorWindow.ImportFolderSelection = () => Path.GetDirectoryName(f.input);
                ClickText("Importar carpeta GLB");
                Require(c.Manifests.Count == f.count + 1 && c.Manifests.TryGetBySavicId(f.id, out m) && m.source.sourceHash == revisedHash,
                    "Importing an archived prior revision duplicated or rolled back the current asset.");

                // A malformed replacement must leave the previously published bytes intact.
                string validPrefabHash = SavicHashService.ComputeSha256(c.Layout.FromProjectRelativePath(prefabPath));
                string invalid = Path.Combine(Path.GetDirectoryName(f.input), "broken_chair.glb");
                File.WriteAllText(invalid, "deliberately malformed replacement");
                f.hashes.Add(SavicHashService.ComputeSha256(invalid)); SavicAtomicFile.WriteJson(State, f);
                Require(EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene), "Could not save fixture before rejected update.");
                bool rejected = false;
                try { new SavicSourceUpdateService(c).Update(f.id, invalid); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Malformed source update was accepted.");
                Require(c.Manifests.TryGetBySavicId(f.id, out m) && m.status == "PUBLISHED" && m.source.sourceHash == revisedHash &&
                    SavicHashService.ComputeSha256(c.Layout.FromProjectRelativePath(prefabPath)) == validPrefabHash,
                    "Failed update did not preserve the last valid source and prefab.");
                Require(SavicSourceUpdateService.ReadLast(c.Layout, f.id).state == "BLOCKED", "Rejected proposal lost its diagnostic state.");
                Require(File.Exists(c.Layout.FromProjectRelativePath(m.sourceRevisions[0].archivedRelativePath)),
                    "Previous original bytes were discarded.");
                ValidateResolvedHistory(m);
                if (prepareRollback)
                {
                    f.recoveryHash = revisedHash; f.recoveryPrefabHash = validPrefabHash;
                    f.itemPath = itemPath; f.prefabPath = prefabPath; f.itemGuid = itemGuid; f.prefabGuid = prefabGuid;
                    WriteVariant(c.Layout.FromProjectRelativePath(m.source.archivedRelativePath), f.input, Guid.NewGuid().ToString("N"));
                    f.hashes.Add(SavicHashService.ComputeSha256(f.input)); SavicAtomicFile.WriteJson(State, f);
                    Require(EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene), "Could not save cold revision fixture.");
                    new SavicSourceUpdateService(c).Update(f.id, f.input);
                    var record = SavicSourceUpdateService.ReadLast(c.Layout, f.id);
                    Require(record.state == "COMMITTED" && record.previousHash == revisedHash, "Second real revision did not commit.");
                    // Inject only the missing final commit marker of this test transaction.
                    // Actual files and backups come from the real update, not fabricated publication.
                    record.state = "PREPARING"; SavicAtomicFile.WriteJson(Path.Combine(c.Layout.SavicRoot, "SourceUpdates", f.id + ".json"), record);
                    SavicAtomicFile.WriteJson(State, f); keep = true;
                    window.Close(); SavicEditorWindow.ImportFolderSelection = null; SavicEditorWindow.SourceRevisionSelection = null;
                    Debug.Log("[SAVIC] EDITOR REVISION COLD CHECKPOINT - PASS: real revision and rollback backups persisted; incomplete final marker injected before actual Editor exit.");
                    EditorApplication.Exit(0); return;
                }
                File.WriteAllText(Path.Combine(c.Layout.LogsRoot, "editor-workflow-real-result.txt"),
                    DateTime.UtcNow.ToString("O") + "\nPASS checks=" + checks +
                    "\nNative UI import/resume/revalidate/update; two actual Editor launches; source revision identity/GUID/manual price/catalog singleton; malformed update rollback; external original retained.\n");
                Debug.Log("[SAVIC] EDITOR WORKFLOW REAL - PASS: " + checks + " checks; cold restart and native UI actions verified.");
            }
            finally { if (!keep) Cleanup(f); }
        }

        public static void RecoverRevisionAfterColdRestartFromCommandLine()
        {
            var c = SavicEditorContext.Instance; var f = SavicAtomicFile.ReadJson<Fixture>(State);
            Require(f != null && !string.IsNullOrEmpty(f.recoveryHash), "Revision checkpoint missing.");
            try
            {
                new SavicSourceUpdateService(c).RecoverInterrupted();
                Require(c.Manifests.TryGetBySavicId(f.id, out var m) && m.status == "PUBLISHED" && m.source.sourceHash == f.recoveryHash,
                    "Cold revision recovery did not restore the valid source and publication.");
                Require(SavicHashService.ComputeSha256(c.Layout.FromProjectRelativePath(f.prefabPath)) == f.recoveryPrefabHash &&
                    AssetDatabase.AssetPathToGUID(f.itemPath) == f.itemGuid && AssetDatabase.AssetPathToGUID(f.prefabPath) == f.prefabGuid,
                    "Cold rollback lost published bytes or GUIDs.");
                var item = AssetDatabase.LoadAssetAtPath<RestaurantPlaceableItemDefinition>(f.itemPath);
                Require(new SerializedObject(item).FindProperty("purchasePrice").intValue == 99917, "Cold rollback lost manual authoring.");
                var record = SavicSourceUpdateService.ReadLast(c.Layout, f.id);
                Require(record.state == "BLOCKED" && File.Exists(c.Layout.FromProjectRelativePath(record.proposedArchive)) &&
                    SavicHashService.ComputeSha256(c.Layout.FromProjectRelativePath(record.proposedArchive)) == record.proposedHash,
                    "Cold recovery discarded the proposed original or diagnostic state.");
                ValidateResolvedHistory(m);
                File.WriteAllText(Path.Combine(c.Layout.LogsRoot, "editor-revision-cold-rollback-result.txt"), DateTime.UtcNow.ToString("O") +
                    "\nPASS: real publication/source/GUID/manual price restored after actual Editor restart; incomplete marker was injected in a real update transaction; proposed bytes retained.\n");
                Debug.Log("[SAVIC] EDITOR REVISION COLD ROLLBACK - PASS: source, publication bytes, GUIDs, manual price, proposal retention and clean current view.");
            }
            finally { Cleanup(f); }
        }
        public static void RunFinalVerificationFromCommandLine()
        {
            SavicV1ClosureGate.RunFromMenu();
            SavicCanonicalContentInventoryProbe.RunFromCommandLine();
            var c = SavicEditorContext.Instance;
            Require(c.Jobs.PendingProcessCount == 0, "A test left pending canonical jobs.");
            Require(c.Manifests.GetAll().All(m => m.status != "NEEDS_REVIEW" && !m.status.StartsWith("FAILED", StringComparison.Ordinal)), "Real inventory still contains unresolved assets.");
            foreach (var m in c.Manifests.GetAll().Where(SavicFunctionalRuntimeAcceptance.Required))
                Require(SavicFunctionalRuntimeAcceptance.Matches(m, c.Layout), "Functional proof is not current: " + m.savicId);
            Debug.Log("[SAVIC] EDITOR OPERATIONS FINAL - PASS: closure gate, actual inventory, idle canonical queue and current functional proofs.");
        }
        private static void ValidateResolvedHistory(SavicManifest m)
        {
            var old = new SavicJobRecord { jobId = "old-failure", manifestSavicId = m.savicId,
                state = SavicJobState.FailedProcessing.ToString(), createdUtc = "2026-10-01T00:00:00Z" };
            var latest = new SavicJobRecord { jobId = "resolved", manifestSavicId = m.savicId,
                state = SavicJobState.Done.ToString(), createdUtc = "2026-10-02T00:00:00Z" };
            var view = SavicEditorReadModel.Build(new[] { m }, new[] { latest, old }, null);
            Require(view.Reviews.Count == 0 && view.Summary.Errors == 0 && view.History.Any(h => h.StableKey.Contains("old-failure")),
                "Resolved failure remains actionable or disappeared from history.");
        }
        private static void Drain(SavicEditorContext c)
        {
            for (int i = 0; i < 48 && c.Jobs.PendingProcessCount > 0; i++)
                c.Batch.TickOneIgnoringCooldownForDiagnostics();
            Require(c.Jobs.PendingProcessCount == 0, "Queue did not drain.");
        }
        private static void ClickText(string text)
        {
            var button = window.rootVisualElement.Query<Button>().ToList().Single(b => b.text == text);
            Require(button.enabledSelf, "Native UI action disabled: " + text);
            Submit(button);
        }
        private static void Submit(Button button)
        {
            button.Focus(); using var submit = NavigationSubmitEvent.GetPooled(); submit.target = button; button.SendEvent(submit);
        }
        private static void SaveScene()
        {
            string path = SavicEditorContext.Instance.Layout.FromProjectRelativePath(Scene);
            Require(!File.Exists(path), "Diagnostic scene already exists; do not overwrite.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Require(EditorSceneManager.SaveScene(scene, Scene), "Could not save diagnostic scene.");
        }
        internal static void WriteVariant(string source, string target, string marker)
        {
            byte[] bytes = File.ReadAllBytes(source);
            Require(BitConverter.ToUInt32(bytes, 0) == 0x46546C67 && BitConverter.ToUInt32(bytes, 16) == 0x4E4F534A, "Real GLB has no JSON chunk.");
            int jsonLength = BitConverter.ToInt32(bytes, 12);
            string json = Encoding.UTF8.GetString(bytes, 20, jsonLength).TrimEnd(' ', '\0', '\r', '\n');
            int end = json.LastIndexOf('}');
            json = System.Text.RegularExpressions.Regex.Replace(json, ",\"savicEditorWorkflowMarker\":\"[a-zA-Z0-9]+\"", "");
            end = json.LastIndexOf('}');
            string changed = json.Substring(0, end) + ",\"savicEditorWorkflowMarker\":\"" + marker + "\"}";
            byte[] payload = Encoding.UTF8.GetBytes(changed.PadRight((changed.Length + 3) / 4 * 4, ' '));
            // JSON is ASCII for this fixture; re-pad bytes for UTF-8 sources as well.
            Array.Resize(ref payload, (payload.Length + 3) / 4 * 4);
            for (int i = Encoding.UTF8.GetByteCount(changed); i < payload.Length; i++) payload[i] = 32;
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(0x46546C67u); writer.Write(2u); writer.Write((uint)(bytes.Length - jsonLength + payload.Length));
            writer.Write((uint)payload.Length); writer.Write(0x4E4F534Au); writer.Write(payload);
            writer.Write(bytes, 20 + jsonLength, bytes.Length - 20 - jsonLength);
            File.WriteAllBytes(target, output.ToArray());
        }
        private static void Cleanup(Fixture f)
        {
            if (window != null) window.Close();
            SavicEditorWindow.ImportFolderSelection = null; SavicEditorWindow.SourceRevisionSelection = null;
            var c = SavicEditorContext.Instance;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(f.id))
            {
                if (c.Manifests.TryGetBySavicId(f.id, out var m))
                    foreach (var a in m.artifacts.Where(a => a != null && a.projectRelativePath.StartsWith("Assets/Generated/BistroBuilder/SAVIC/Published/", StringComparison.Ordinal)))
                        AssetDatabase.DeleteAsset(a.projectRelativePath);
                if (c.Manifests.TryGetManifestPath(f.id, out string manifestPath)) File.Delete(manifestPath);
                string request = Path.Combine(c.Layout.SavicRoot, "SourceUpdates", f.id + ".json");
                if (File.Exists(request)) File.Delete(request);
            }
            foreach (string hash in f.hashes.Distinct())
            {
                string mirrorRoot = Path.GetDirectoryName(c.Layout.GetUnitySourceMirrorPath(hash, f.sourceName));
                AssetDatabase.DeleteAsset(c.Layout.ToProjectRelativePath(mirrorRoot));
                string archiveRoot = Path.GetDirectoryName(c.Layout.GetArchivedSourcePath(hash, f.sourceName));
                if (!Path.GetFullPath(archiveRoot).StartsWith(Path.GetFullPath(c.Layout.ContentSourceRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Diagnostic archive path escaped the source root.");
                if (Directory.Exists(archiveRoot)) Directory.Delete(archiveRoot, true);
            }
            AssetDatabase.DeleteAsset(Scene);
            File.WriteAllBytes(c.Layout.FromProjectRelativePath(Catalog), Convert.FromBase64String(f.catalog));
            File.WriteAllBytes(c.Layout.FromProjectRelativePath(Catalog + ".meta"), Convert.FromBase64String(f.catalogMeta));
            if (f.queueExisted) File.WriteAllText(c.Layout.QueueSnapshotPath, f.queue);
            else if (File.Exists(c.Layout.QueueSnapshotPath)) File.Delete(c.Layout.QueueSnapshotPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); c.Manifests.Reload(); c.Jobs.Reload();
            Require(c.Manifests.Count == f.count && SavicHashService.ComputeSha256(c.Layout.FromProjectRelativePath(f.originalArchive)) == f.originalHash,
                "Diagnostic cleanup changed existing assets.");
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); checks++; }
    }
}
