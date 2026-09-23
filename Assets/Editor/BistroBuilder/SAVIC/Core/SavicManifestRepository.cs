using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicManifestRepository
    {
        private readonly SavicStorageLayout layout;
        private readonly Dictionary<string, SavicManifest> bySourceHash =
            new Dictionary<string, SavicManifest>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> pathBySavicId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, SavicManifest> bySavicId =
            new Dictionary<string, SavicManifest>(StringComparer.OrdinalIgnoreCase);

        private bool loaded;

        internal SavicManifestRepository(SavicStorageLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        internal event Action Changed;

        internal int Count
        {
            get
            {
                EnsureLoaded();
                return bySourceHash.Count;
            }
        }

        internal bool TryGetBySourceHash(string sourceHash, out SavicManifest manifest)
        {
            EnsureLoaded();
            return bySourceHash.TryGetValue(sourceHash ?? string.Empty, out manifest);
        }

        internal bool TryGetBySavicId(
            string savicId,
            out SavicManifest manifest)
        {
            EnsureLoaded();
            return bySavicId.TryGetValue(
                savicId ?? string.Empty,
                out manifest);
        }

        internal IReadOnlyList<SavicManifest> GetAll()
        {
            EnsureLoaded();

            List<SavicManifest> manifests =
                new List<SavicManifest>(bySavicId.Values);

            manifests.Sort(
                (left, right) =>
                    string.Compare(
                        left?.createdUtc,
                        right?.createdUtc,
                        StringComparison.Ordinal));

            return manifests;
        }

        internal bool TryGetManifestPath(
            string savicId,
            out string manifestPath)
        {
            EnsureLoaded();
            return pathBySavicId.TryGetValue(
                savicId ?? string.Empty,
                out manifestPath);
        }

        internal SavicManifest CreateIngested(
            string sourceHash,
            string originalFileName,
            string archivedAbsolutePath,
            long byteLength,
            long originalLastWriteUtcTicks,
            SavicSourceKind sourceKind)
        {
            if (TryGetBySourceHash(sourceHash, out SavicManifest existing))
                return existing;

            string now = DateTime.UtcNow.ToString("O");
            SavicManifest manifest = new SavicManifest
            {
                savicId = Guid.NewGuid().ToString("N"),
                createdUtc = now,
                updatedUtc = now,
                source = new SavicSourceRecord
                {
                    sourceHash = sourceHash,
                    originalFileName = originalFileName ?? string.Empty,
                    extension = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant(),
                    sourceKind = sourceKind.ToString(),
                    archivedRelativePath = layout.ToProjectRelativePath(archivedAbsolutePath),
                    byteLength = byteLength,
                    originalLastWriteUtcTicks = originalLastWriteUtcTicks,
                    ingestedUtc = now
                }
            };

            Save(manifest);
            return manifest;
        }

        internal void Save(SavicManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrWhiteSpace(manifest.savicId))
                throw new InvalidOperationException("Manifest has no SavicId.");
            if (manifest.source == null || string.IsNullOrWhiteSpace(manifest.source.sourceHash))
                throw new InvalidOperationException("Manifest has no source hash.");

            EnsureLoaded();

            if (bySourceHash.TryGetValue(
                    manifest.source.sourceHash,
                    out SavicManifest sourceOwner) &&
                !string.Equals(
                    sourceOwner.savicId,
                    manifest.savicId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SourceHash already belongs to another SAVIC identity.");
            }

            if (bySavicId.TryGetValue(
                    manifest.savicId,
                    out SavicManifest idOwner) &&
                idOwner?.source != null &&
                !string.Equals(
                    idOwner.source.sourceHash,
                    manifest.source.sourceHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "SavicId already belongs to another source.");
            }

            manifest.schemaVersion = SavicVersion.ManifestSchemaVersion;
            manifest.savicVersion = SavicVersion.ProductVersion;
            manifest.updatedUtc = DateTime.UtcNow.ToString("O");

            string path = GetManifestPath(manifest.savicId);
            SavicAtomicFile.WriteJson(path, manifest);

            loaded = true;
            bySourceHash[manifest.source.sourceHash] = manifest;
            bySavicId[manifest.savicId] = manifest;
            pathBySavicId[manifest.savicId] = path;
            NotifyChanged();
        }

        internal void Reload()
        {
            loaded = false;
            bySourceHash.Clear();
            bySavicId.Clear();
            pathBySavicId.Clear();
            EnsureLoaded();
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[SAVIC] Manifest change listener failed safely: " +
                    exception);
            }
        }

        private void EnsureLoaded()
        {
            if (loaded)
                return;

            layout.EnsureInfrastructure();
            bySourceHash.Clear();
            bySavicId.Clear();
            pathBySavicId.Clear();

            string[] paths = Directory.GetFiles(
                layout.ManifestsRoot,
                "*.json",
                SearchOption.TopDirectoryOnly);

            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                try
                {
                    SavicManifest manifest = SavicAtomicFile.ReadJson<SavicManifest>(path);
                    if (manifest?.source == null ||
                        string.IsNullOrWhiteSpace(manifest.savicId) ||
                        string.IsNullOrWhiteSpace(manifest.source.sourceHash))
                    {
                        Debug.LogWarning("[SAVIC] Ignoring invalid manifest: " + path);
                        continue;
                    }

                    if (bySourceHash.ContainsKey(manifest.source.sourceHash))
                    {
                        Debug.LogError(
                            "[SAVIC] Duplicate SourceHash in manifests: " +
                            manifest.source.sourceHash + " (" + path + ")");
                        continue;
                    }

                    if (bySavicId.ContainsKey(manifest.savicId))
                    {
                        Debug.LogError(
                            "[SAVIC] Duplicate SavicId in manifests: " +
                            manifest.savicId + " (" + path + ")");
                        continue;
                    }

                    bySourceHash.Add(manifest.source.sourceHash, manifest);
                    bySavicId.Add(manifest.savicId, manifest);
                    pathBySavicId[manifest.savicId] = path;
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[SAVIC] Failed to read manifest '" + path + "': " +
                        exception.Message);
                }
            }

            loaded = true;
        }

        private string GetManifestPath(string savicId)
        {
            return Path.Combine(layout.ManifestsRoot, savicId + ".json");
        }
    }
}
