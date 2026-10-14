using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable] internal sealed class SavicSourceUpdateRecord
    {
        public string savicId, state, message, previousHash, proposedHash, proposedArchive;
        public SavicManifest previous;
        public List<SavicSourceUpdateFile> files = new List<SavicSourceUpdateFile>();
        public List<string> publicationFolders = new List<string>();
    }
    [Serializable] internal sealed class SavicSourceUpdateFile
    { public string path, backup, hash; public bool existed; }

    // A durable publication rollback, not a second content catalog.
    // Original bytes and previous authoring remain recoverable after a cold restart.
    internal sealed class SavicSourceUpdateService
    {
        private const string CatalogPath = "Assets/Data/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalog_Main.asset";
        private readonly SavicEditorContext context;
        internal SavicSourceUpdateService(SavicEditorContext context) { this.context = context; }
        private static string RecordPath(SavicStorageLayout layout, string id)
        {
            if (string.IsNullOrEmpty(id) || id.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
                throw new InvalidOperationException("Invalid SAVIC identity.");
            return Path.Combine(layout.SavicRoot, "SourceUpdates", id + ".json");
        }
        internal static SavicSourceUpdateRecord ReadLast(SavicStorageLayout layout, string id)
            => SavicAtomicFile.ReadJson<SavicSourceUpdateRecord>(RecordPath(layout, id));

        internal void Update(string id, string incoming)
        {
            var actions = new SavicEditorActionService(context.Layout, context.Manifests, context.Jobs);
            if (!actions.CanProcess(id, out string error)) throw new InvalidOperationException(error);
            if (context.Jobs.PendingProcessCount != 0 || context.Intake.IsBusy)
                throw new InvalidOperationException("Espera a que termine la cola antes de actualizar un original.");
            context.Manifests.TryGetBySavicId(id, out var current);
            actions.VerifySource(current);
            string extension = Path.GetExtension(incoming).ToLowerInvariant();
            if (extension != ".glb" && extension != ".fbx")
                throw new InvalidOperationException("Selecciona un modelo GLB o FBX autocontenido. GLTF con archivos externos aún no admite esta actualización.");
            SavicRuntimeVerificationSession.RequireSavedScenes();
            var before = new FileInfo(incoming);
            long length = before.Length, write = before.LastWriteTimeUtc.Ticks;
            string hash = SavicHashService.ComputeSha256(incoming);
            before.Refresh();
            if (before.Length != length || before.LastWriteTimeUtc.Ticks != write)
                throw new InvalidOperationException("El archivo sigue cambiando. Espera a que termine la copia.");
            if (hash == current.source.sourceHash) { actions.Reprocess(id); return; }
            if (context.Manifests.TryGetBySourceHash(hash, out var owner) && owner.savicId != id)
                throw new InvalidOperationException("Esta fuente pertenece a otro asset de SAVIC. No se fusionan identidades automáticamente.");
            if (context.Manifests.TryGetArchivedRevision(hash, out var previousOwner, out _) && previousOwner.savicId != id)
                throw new InvalidOperationException("Esta fuente es una revisión de otro asset; no se fusionan identidades.");
            string archive = context.Layout.GetArchivedSourcePath(hash, Path.GetFileName(incoming));
            Directory.CreateDirectory(Path.GetDirectoryName(archive));
            if (!File.Exists(archive)) File.Copy(incoming, archive);
            if (SavicHashService.ComputeSha256(archive) != hash)
                throw new InvalidOperationException("La copia archivada no coincide con la fuente.");
            var record = new SavicSourceUpdateRecord { savicId = id, state = "PREPARING",
                message = "Actualización en preparación.", previousHash = current.source.sourceHash, proposedHash = hash,
                proposedArchive = context.Layout.ToProjectRelativePath(archive),
                previous = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(current)) };
            string backupRoot = Path.Combine(context.Layout.CacheRoot, "SourceUpdates", id, hash);
            Directory.CreateDirectory(backupRoot);
            context.Manifests.TryGetManifestPath(id, out string manifestPath);
            Capture(record, context.Layout.ToProjectRelativePath(manifestPath), backupRoot);
            Capture(record, context.Layout.ToProjectRelativePath(context.Layout.QueueSnapshotPath), backupRoot);
            Capture(record, CatalogPath, backupRoot); Capture(record, CatalogPath + ".meta", backupRoot);
            foreach (var a in current.artifacts.Where(a => a != null && !string.IsNullOrEmpty(a.projectRelativePath) &&
                a.projectRelativePath.StartsWith("Assets/", StringComparison.Ordinal) && a.role != "unity.source_mirror"))
            {
                Capture(record, a.projectRelativePath, backupRoot);
                Capture(record, a.projectRelativePath + ".meta", backupRoot);
                string folder = Path.GetDirectoryName(a.projectRelativePath).Replace('\\', '/');
                if (folder.StartsWith("Assets/Generated/BistroBuilder/SAVIC/Published/", StringComparison.Ordinal) &&
                    !record.publicationFolders.Contains(folder))
                    record.publicationFolders.Add(folder);
            }
            AssetDatabase.ReleaseCachedFileHandles();
            foreach (string folder in record.publicationFolders)
                if (Directory.Exists(context.Layout.FromProjectRelativePath(folder)))
                    foreach (string file in Directory.GetFiles(context.Layout.FromProjectRelativePath(folder), "*", SearchOption.AllDirectories))
                        Capture(record, context.Layout.ToProjectRelativePath(file), backupRoot);
            foreach (string report in new[] { current.barCounterRuntime?.reportRelativePath, current.barStoolRuntime?.reportRelativePath, current.overheadEquipmentRuntime?.reportRelativePath })
                if (!string.IsNullOrEmpty(report)) Capture(record, report, backupRoot);
            Save(record);
            try
            {
                var next = JsonUtility.FromJson<SavicManifest>(JsonUtility.ToJson(current));
                next.sourceRevisions ??= new List<SavicSourceRecord>();
                next.sourceRevisions.Add(JsonUtility.FromJson<SavicSourceRecord>(JsonUtility.ToJson(current.source)));
                next.source = new SavicSourceRecord { sourceHash = hash, originalFileName = Path.GetFileName(incoming),
                    extension = extension, sourceKind = SavicSourceKind.Model3D.ToString(), archivedRelativePath = record.proposedArchive,
                    byteLength = length, originalLastWriteUtcTicks = write, ingestedUtc = DateTime.UtcNow.ToString("O") };
                // Newly archived bytes are not yet a published revision. The durable journal owns restoration of the previous publication.
                next.status = "INGESTED";
                context.Manifests.ReplaceSourceRevision(next, record.previousHash);
                var outcome = Process(next);
                context.Manifests.TryGetBySavicId(id, out next);
                if (next.type != record.previous.type && !string.IsNullOrEmpty(record.previous.canonicalContentId))
                    throw new InvalidOperationException("La nueva fuente cambia la función del asset. Se conserva la versión publicada anterior.");
                if (outcome.Succeeded) { Commit(record); return; }
                if (SavicFunctionalRuntimeAcceptance.Required(next) && next.status == "NEEDS_REVIEW" &&
                    outcome.Diagnostics?.reasonCode?.EndsWith("_RUNTIME_ACCEPTANCE_PENDING", StringComparison.Ordinal) == true)
                {
                    record.state = "VERIFYING"; record.message = "Verificando la nueva versión; la publicación anterior tiene rollback persistente."; Save(record);
                    SavicRuntimeVerificationSession.Run(id); return;
                }
                throw new InvalidOperationException(outcome.Message);
            }
            catch (Exception e) { Rollback(record, e.Message); throw; }
        }

        private SavicSourceProcessingOutcome Process(SavicManifest manifest)
        {
            bool paused = context.Jobs.IsPaused;
            var originalScene = SceneManager.GetActiveScene();
            var workingScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(workingScene);
            try
            {
                context.Jobs.SetPaused(false);
                var queued = context.Jobs.RequeueVerifiedAsset(manifest);
                if (!context.Jobs.TryClaimNextProcessable(out var claimed) || claimed.jobId != queued.jobId)
                    throw new InvalidOperationException("La cola cambió antes de procesar la actualización.");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var outcome = context.SourceProcessing.ProcessBySavicId(manifest.savicId);
                context.Jobs.CompleteProcessing(claimed.jobId, outcome, timer.ElapsedMilliseconds);
                return outcome;
            }
            finally
            {
                if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                if (workingScene.IsValid() && workingScene.isLoaded) EditorSceneManager.CloseScene(workingScene, true);
                context.Jobs.SetPaused(paused);
            }
        }

        internal void FinishVerification(string id, bool success, string message)
        {
            var record = ReadLast(context.Layout, id);
            if (record == null || record.state != "VERIFYING") return;
            try
            {
                if (!success) { Rollback(record, message); return; }
                context.Manifests.TryGetBySavicId(id, out var m);
                if (m.source.sourceHash != record.proposedHash || !SavicFunctionalRuntimeAcceptance.Matches(m, context.Layout))
                    throw new InvalidOperationException("La aceptación no corresponde a la revisión propuesta.");
                var result = Process(m);
                if (!result.Succeeded) throw new InvalidOperationException(result.Message);
                Commit(record);
            }
            catch (Exception e) { Rollback(record, e.Message); throw; }
        }

        internal void RecoverInterrupted()
        {
            string root = Path.Combine(context.Layout.SavicRoot, "SourceUpdates");
            if (!Directory.Exists(root) || SavicRuntimeVerificationSession.IsActive) return;
            foreach (string path in Directory.GetFiles(root, "*.json"))
            {
                var record = SavicAtomicFile.ReadJson<SavicSourceUpdateRecord>(path);
                if (record?.state == "PREPARING" || record?.state == "VERIFYING")
                    Rollback(record, "Unity se cerró durante la actualización. Se restauró la versión anterior; la propuesta archivada sigue disponible.");
            }
        }

        internal void RestoreLastForDiagnostics(string id) => Rollback(ReadLast(context.Layout, id), "Diagnostic fixture rollback.");
        private void Commit(SavicSourceUpdateRecord record)
        {
            if (!context.Manifests.TryGetBySavicId(record.savicId, out var m) || m.source.sourceHash != record.proposedHash ||
                m.status != "PUBLISHED" || (!string.IsNullOrEmpty(record.previous.canonicalContentId) && m.canonicalContentId != record.previous.canonicalContentId))
                throw new InvalidOperationException("La actualización no conserva publicación e identidad.");
            record.state = "COMMITTED"; record.message = "Original actualizado conservando la identidad y el catálogo."; Save(record);
        }

        private void Capture(SavicSourceUpdateRecord record, string relative, string root)
        {
            if (record.files.Any(f => f.path == relative)) return;
            string absolute = CheckedPath(relative);
            var file = new SavicSourceUpdateFile { path = relative, existed = File.Exists(absolute) };
            if (file.existed)
            {
                string backup = Path.Combine(root, record.files.Count.ToString("D4") + ".bak");
                File.Copy(absolute, backup, true); file.backup = context.Layout.ToProjectRelativePath(backup);
                file.hash = SavicHashService.ComputeSha256(backup);
            }
            record.files.Add(file);
        }
        private string CheckedPath(string relative)
        {
            string absolute = context.Layout.FromProjectRelativePath(relative);
            relative = context.Layout.ToProjectRelativePath(absolute);
            if (!(relative.StartsWith("Assets/", StringComparison.Ordinal) ||
                relative.StartsWith("SAVIC/Manifests/", StringComparison.Ordinal) ||
                relative.StartsWith("Library/BistroBuilder/SAVIC/Logs/", StringComparison.Ordinal) ||
                relative == context.Layout.ToProjectRelativePath(context.Layout.QueueSnapshotPath)))
                throw new InvalidOperationException("La transacción intenta modificar una ruta fuera de su contrato.");
            return absolute;
        }
        private void Rollback(SavicSourceUpdateRecord record, string message)
        {
            if (record == null || record.previous == null || record.savicId != record.previous.savicId)
                throw new InvalidOperationException("Registro de actualización inválido.");
            foreach (string folder in record.publicationFolders)
            {
                string normalized = context.Layout.ToProjectRelativePath(CheckedPath(folder));
                if (!normalized.StartsWith("Assets/Generated/BistroBuilder/SAVIC/Published/", StringComparison.Ordinal))
                    throw new InvalidOperationException("El rollback intenta salir de su publicación.");
            }
            foreach (var file in record.files) CheckedPath(file.path);
            // Validate all backups before any restoration.
            foreach (var f in record.files.Where(f => f.existed))
            {
                string backup = context.Layout.FromProjectRelativePath(f.backup);
                if (!context.Layout.ToProjectRelativePath(backup).StartsWith("Library/BistroBuilder/SAVIC/Cache/SourceUpdates/", StringComparison.Ordinal) ||
                    !File.Exists(backup) || SavicHashService.ComputeSha256(backup) != f.hash)
                    throw new IOException("Falta una copia íntegra de rollback. Se conserva el registro para recuperar: " + f.path);
                CheckedPath(f.path);
            }
            AssetDatabase.ReleaseCachedFileHandles();
            foreach (string folder in record.publicationFolders)
                if (Directory.Exists(context.Layout.FromProjectRelativePath(folder)))
                    foreach (string absolute in Directory.GetFiles(context.Layout.FromProjectRelativePath(folder), "*", SearchOption.AllDirectories))
                    {
                        string relative = context.Layout.ToProjectRelativePath(absolute);
                        if (!record.files.Any(f => f.path == relative)) File.Delete(CheckedPath(relative));
                    }
            foreach (var f in record.files)
            {
                string absolute = CheckedPath(f.path);
                if (f.existed) { Directory.CreateDirectory(Path.GetDirectoryName(absolute)); File.Copy(context.Layout.FromProjectRelativePath(f.backup), absolute, true); }
                else if (File.Exists(absolute)) File.Delete(absolute);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            context.Manifests.Reload(); context.Jobs.Reload();
            record.state = "BLOCKED"; record.message = message + " La propuesta se conserva en " + record.proposedArchive;
            Save(record);
        }
        private void Save(SavicSourceUpdateRecord record) => SavicAtomicFile.WriteJson(RecordPath(context.Layout, record.savicId), record);
    }
}
