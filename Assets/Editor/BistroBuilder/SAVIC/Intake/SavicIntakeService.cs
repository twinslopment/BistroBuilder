using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicIntakeService
    {
        private const int RequiredStableObservations = 2;
        private const double MinimumFileAgeSeconds = 1.0;
        private const int MaximumHashAttempts = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".fbx", ".glb", ".gltf", ".obj",
                ".png", ".jpg", ".jpeg", ".webp", ".tga", ".psd",
                ".json", ".csv"
            };

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;
        private readonly SavicJobStore jobs;
        private readonly Dictionary<string, Observation> observations =
            new Dictionary<string, Observation>(StringComparer.OrdinalIgnoreCase);

        private PendingHash pendingHash;

        internal SavicIntakeService(
            SavicStorageLayout layout,
            SavicManifestRepository manifests,
            SavicJobStore jobs)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.manifests = manifests ?? throw new ArgumentNullException(nameof(manifests));
            this.jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        }

        internal bool IsBusy => pendingHash != null;
        internal string PendingFileName =>
            pendingHash == null ? string.Empty : Path.GetFileName(pendingHash.Path);

        internal int GetInboxFileCount()
        {
            layout.EnsureInfrastructure();
            return Directory.EnumerateFiles(layout.DropHereRoot)
                .Count(IsSupportedCandidate);
        }

        internal void Tick()
        {
            layout.EnsureInfrastructure();

            if (pendingHash != null)
            {
                TryCompletePendingHash();
                return;
            }

            ReconcileObservationsAndSchedule();
        }

        internal SavicIntakeOutcome IngestSynchronously(string path)
        {
            FileSnapshot snapshot = CaptureSnapshot(path);
            string hash = SavicHashService.ComputeSha256(path);
            FileSnapshot afterHash = CaptureSnapshot(path);

            if (!snapshot.Equals(afterHash))
            {
                return new SavicIntakeOutcome(
                    false,
                    false,
                    string.Empty,
                    string.Empty,
                    "The source changed while it was being hashed.");
            }

            return CommitIngest(path, snapshot, hash);
        }

        private void ReconcileObservationsAndSchedule()
        {
            string[] files = Directory.GetFiles(
                layout.DropHereRoot,
                "*",
                SearchOption.TopDirectoryOnly);

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            HashSet<string> current =
                new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);

            string[] staleObservationKeys = observations.Keys
                .Where(path => !current.Contains(path))
                .ToArray();

            foreach (string stale in staleObservationKeys)
                observations.Remove(stale);

            DateTime now = DateTime.UtcNow;

            foreach (string path in files)
            {
                if (!IsSupportedCandidate(path))
                    continue;

                FileSnapshot snapshot;
                try
                {
                    snapshot = CaptureSnapshot(path);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if (!observations.TryGetValue(path, out Observation observation))
                {
                    observation = new Observation(snapshot);
                    observations.Add(path, observation);
                    continue;
                }

                observation.Observe(snapshot);

                if (now < observation.NextRetryUtc ||
                    observation.StableObservations < RequiredStableObservations ||
                    now - snapshot.LastWriteUtc < TimeSpan.FromSeconds(MinimumFileAgeSeconds) ||
                    !SavicHashService.CanAcquireExclusiveRead(path))
                {
                    continue;
                }

                StartHash(path, snapshot);
                return;
            }
        }

        private void StartHash(string path, FileSnapshot snapshot)
        {
            pendingHash = new PendingHash(
                path,
                snapshot,
                Task.Run(() => SavicHashService.ComputeSha256(path)));
        }

        private void TryCompletePendingHash()
        {
            PendingHash current = pendingHash;
            if (current == null || !current.Task.IsCompleted)
                return;

            pendingHash = null;

            if (current.Task.IsCanceled || current.Task.IsFaulted)
            {
                Exception error = current.Task.Exception?.GetBaseException();
                HandleHashFailure(current.Path, error?.Message ?? "Hashing was cancelled.");
                return;
            }

            try
            {
                if (!File.Exists(current.Path))
                {
                    observations.Remove(current.Path);
                    return;
                }

                FileSnapshot afterHash = CaptureSnapshot(current.Path);
                if (!current.Snapshot.Equals(afterHash))
                {
                    observations[current.Path] = new Observation(afterHash);
                    return;
                }

                SavicIntakeOutcome outcome =
                    CommitIngest(current.Path, current.Snapshot, current.Task.Result);

                observations.Remove(current.Path);

                if (!outcome.Succeeded)
                    Debug.LogError("[SAVIC] Intake failed: " + outcome.Message);
                else if (outcome.DuplicateExact)
                    Debug.Log("[SAVIC] Exact duplicate ignored safely: " + Path.GetFileName(current.Path));
                else
                    Debug.Log("[SAVIC] Source ingested: " + Path.GetFileName(current.Path));
            }
            catch (Exception exception)
            {
                HandleHashFailure(current.Path, exception.Message);
            }
        }

        private SavicIntakeOutcome CommitIngest(
            string incomingPath,
            FileSnapshot snapshot,
            string sourceHash)
        {
            if (string.IsNullOrWhiteSpace(sourceHash))
                throw new InvalidOperationException("Hash service returned an empty hash.");

            if (manifests.TryGetBySourceHash(sourceHash, out SavicManifest existing))
            {
                EnsureExistingArchive(existing, incomingPath, snapshot, sourceHash);

                jobs.RecordIngested(
                    existing,
                    true,
                    "Exact duplicate detected by SHA-256. Existing SAVIC identity preserved.");

                DeleteIncomingAfterCommit(incomingPath);

                return new SavicIntakeOutcome(
                    true,
                    true,
                    sourceHash,
                    existing.savicId,
                    "Exact duplicate.");
            }

            string archivePath = layout.GetArchivedSourcePath(
                sourceHash,
                snapshot.FileName);

            EnsureArchiveFromIncoming(
                incomingPath,
                archivePath,
                snapshot.Length,
                sourceHash);

            SavicManifest manifest = manifests.CreateIngested(
                sourceHash,
                snapshot.FileName,
                archivePath,
                snapshot.Length,
                snapshot.LastWriteUtc.Ticks,
                ResolveSourceKind(snapshot.Extension));

            jobs.RecordIngested(
                manifest,
                false,
                "Source archive, manifest and queue record committed.");

            DeleteIncomingAfterCommit(incomingPath);

            return new SavicIntakeOutcome(
                true,
                false,
                sourceHash,
                manifest.savicId,
                "Ingested.");
        }

        private void EnsureExistingArchive(
            SavicManifest manifest,
            string incomingPath,
            FileSnapshot snapshot,
            string sourceHash)
        {
            if (manifest?.source == null)
                throw new InvalidOperationException("Existing manifest has no source record.");

            string archivedPath =
                layout.FromProjectRelativePath(manifest.source.archivedRelativePath);

            string repairedArchivePath =
                !string.IsNullOrWhiteSpace(archivedPath)
                    ? archivedPath
                    : layout.GetArchivedSourcePath(
                        sourceHash,
                        string.IsNullOrWhiteSpace(manifest.source.originalFileName)
                            ? snapshot.FileName
                            : manifest.source.originalFileName);

            EnsureArchiveFromIncoming(
                incomingPath,
                repairedArchivePath,
                snapshot.Length,
                sourceHash);

            string repairedRelativePath =
                layout.ToProjectRelativePath(repairedArchivePath);

            if (!string.Equals(
                    manifest.source.archivedRelativePath,
                    repairedRelativePath,
                    StringComparison.Ordinal) ||
                manifest.source.byteLength != snapshot.Length)
            {
                manifest.source.archivedRelativePath = repairedRelativePath;
                manifest.source.byteLength = snapshot.Length;
                manifests.Save(manifest);
            }
        }

        private static void EnsureArchiveFromIncoming(
            string incomingPath,
            string archivePath,
            long expectedLength,
            string expectedSourceHash)
        {
            string archiveDirectory = Path.GetDirectoryName(archivePath)
                ?? throw new InvalidOperationException(
                    "Archive directory could not be resolved.");

            Directory.CreateDirectory(archiveDirectory);

            if (IsArchiveHealthy(
                    archivePath,
                    expectedLength,
                    expectedSourceHash))
            {
                return;
            }

            string tempPath =
                archivePath + ".incoming." + Guid.NewGuid().ToString("N");

            string corruptBackup =
                archivePath + ".corrupt." +
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

            try
            {
                File.Copy(incomingPath, tempPath, false);

                FileInfo staged = new FileInfo(tempPath);
                if (staged.Length != expectedLength ||
                    !string.Equals(
                        SavicHashService.ComputeSha256(tempPath),
                        expectedSourceHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        "Staged source archive failed integrity verification.");
                }

                if (File.Exists(archivePath))
                {
                    ReplaceArchivePreservingPrevious(
                        tempPath,
                        archivePath,
                        corruptBackup);

                    Debug.LogWarning(
                        "[SAVIC] Replaced an invalid source archive. " +
                        "Preserved previous bytes at: " + corruptBackup);
                }
                else
                {
                    File.Move(tempPath, archivePath);
                }

                if (!IsArchiveHealthy(
                        archivePath,
                        expectedLength,
                        expectedSourceHash))
                {
                    throw new IOException(
                        "Committed source archive failed integrity verification.");
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private static bool IsArchiveHealthy(
            string archivePath,
            long expectedLength,
            string expectedSourceHash)
        {
            if (string.IsNullOrWhiteSpace(archivePath) ||
                !File.Exists(archivePath))
            {
                return false;
            }

            FileInfo existing = new FileInfo(archivePath);
            return existing.Length == expectedLength &&
                   string.Equals(
                       SavicHashService.ComputeSha256(archivePath),
                       expectedSourceHash,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void ReplaceArchivePreservingPrevious(
            string stagedPath,
            string archivePath,
            string backupPath)
        {
            try
            {
                File.Replace(
                    stagedPath,
                    archivePath,
                    backupPath,
                    true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceArchiveFallback(
                    stagedPath,
                    archivePath,
                    backupPath);
            }
            catch (IOException)
            {
                ReplaceArchiveFallback(
                    stagedPath,
                    archivePath,
                    backupPath);
            }
        }

        private static void ReplaceArchiveFallback(
            string stagedPath,
            string archivePath,
            string backupPath)
        {
            File.Copy(archivePath, backupPath, true);

            try
            {
                File.Copy(stagedPath, archivePath, true);
                File.Delete(stagedPath);
            }
            catch
            {
                if (File.Exists(backupPath))
                    File.Copy(backupPath, archivePath, true);

                throw;
            }
        }

        private static void DeleteIncomingAfterCommit(string incomingPath)
        {
            if (!File.Exists(incomingPath))
                return;

            File.Delete(incomingPath);
        }

        private void HandleHashFailure(string path, string message)
        {
            if (!observations.TryGetValue(path, out Observation observation))
            {
                if (!File.Exists(path))
                    return;

                observation = new Observation(CaptureSnapshot(path));
                observations[path] = observation;
            }

            observation.Failures++;
            observation.NextRetryUtc = DateTime.UtcNow + RetryDelay;

            if (observation.Failures < MaximumHashAttempts)
            {
                Debug.LogWarning(
                    "[SAVIC] Could not hash '" + Path.GetFileName(path) +
                    "'. Retry " + observation.Failures + "/" + MaximumHashAttempts +
                    ". " + message);
                return;
            }

            Quarantine(path, observation.Failures, message);
            observations.Remove(path);
        }

        private void Quarantine(string path, int attempts, string message)
        {
            string fileName = Path.GetFileName(path);
            string quarantineDirectory = Path.Combine(
                layout.ContentSourceRoot,
                "_Quarantine",
                DateTime.UtcNow.ToString("yyyyMMdd"));

            Directory.CreateDirectory(quarantineDirectory);

            string destination = Path.Combine(
                quarantineDirectory,
                DateTime.UtcNow.ToString("HHmmssfff") + "_" + fileName);

            string archivedRelative = string.Empty;

            try
            {
                File.Move(path, destination);
                archivedRelative = layout.ToProjectRelativePath(destination);
            }
            catch (Exception exception)
            {
                message += " Quarantine move also failed: " + exception.Message;
            }

            jobs.RecordFailure(
                fileName,
                string.Empty,
                archivedRelative,
                SavicJobState.Quarantined,
                attempts,
                message);

            Debug.LogError(
                "[SAVIC] Source quarantined after repeated intake failures: " +
                fileName + ". " + message);
        }

        private static bool IsSupportedCandidate(string path)
        {
            string fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.StartsWith(".", StringComparison.Ordinal) ||
                fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return SupportedExtensions.Contains(Path.GetExtension(path));
        }

        private static SavicSourceKind ResolveSourceKind(string extension)
        {
            if (string.Equals(extension, ".fbx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".obj", StringComparison.OrdinalIgnoreCase))
            {
                return SavicSourceKind.Model3D;
            }

            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".webp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".tga", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".psd", StringComparison.OrdinalIgnoreCase))
            {
                return SavicSourceKind.Image;
            }

            if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            {
                return SavicSourceKind.StructuredData;
            }

            return SavicSourceKind.Unknown;
        }

        private static FileSnapshot CaptureSnapshot(string path)
        {
            FileInfo info = new FileInfo(path);
            info.Refresh();

            if (!info.Exists)
                throw new FileNotFoundException("Source disappeared during intake.", path);

            return new FileSnapshot(
                info.Name,
                info.Extension,
                info.Length,
                info.LastWriteTimeUtc);
        }

        private sealed class Observation
        {
            internal Observation(FileSnapshot snapshot)
            {
                Snapshot = snapshot;
                StableObservations = 1;
            }

            internal FileSnapshot Snapshot;
            internal int StableObservations;
            internal int Failures;
            internal DateTime NextRetryUtc;

            internal void Observe(FileSnapshot snapshot)
            {
                if (Snapshot.Equals(snapshot))
                {
                    StableObservations++;
                    return;
                }

                Snapshot = snapshot;
                StableObservations = 1;
                Failures = 0;
                NextRetryUtc = DateTime.MinValue;
            }
        }

        private sealed class PendingHash
        {
            internal PendingHash(
                string path,
                FileSnapshot snapshot,
                Task<string> task)
            {
                Path = path;
                Snapshot = snapshot;
                Task = task;
            }

            internal string Path { get; }
            internal FileSnapshot Snapshot { get; }
            internal Task<string> Task { get; }
        }

        private readonly struct FileSnapshot : IEquatable<FileSnapshot>
        {
            internal FileSnapshot(
                string fileName,
                string extension,
                long length,
                DateTime lastWriteUtc)
            {
                FileName = fileName;
                Extension = extension;
                Length = length;
                LastWriteUtc = lastWriteUtc;
            }

            internal string FileName { get; }
            internal string Extension { get; }
            internal long Length { get; }
            internal DateTime LastWriteUtc { get; }

            public bool Equals(FileSnapshot other)
            {
                return Length == other.Length &&
                       LastWriteUtc.Ticks == other.LastWriteUtc.Ticks &&
                       string.Equals(
                           FileName,
                           other.FileName,
                           StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is FileSnapshot other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + Length.GetHashCode();
                    hash = hash * 31 + LastWriteUtc.Ticks.GetHashCode();
                    hash = hash * 31 +
                           StringComparer.OrdinalIgnoreCase.GetHashCode(FileName ?? string.Empty);
                    return hash;
                }
            }
        }
    }
}
