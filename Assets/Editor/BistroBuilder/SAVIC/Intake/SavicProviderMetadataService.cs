using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicProviderMetadataBatch
    {
        public int schemaVersion = 1;
        public List<SavicProviderMetadataInput> records = new List<SavicProviderMetadataInput>();
    }

    [Serializable]
    internal sealed class SavicProviderMetadataInput
    {
        public string downloadedOriginalPath = string.Empty;
        public string taskId = string.Empty;
        public string caption = string.Empty;
        public string captionKind = string.Empty;
        public string description = string.Empty;
        public string observedPreviewUrl = string.Empty;
    }

    [Serializable]
    internal sealed class SavicProviderMetadataRecord
    {
        public int schemaVersion = 1;
        public string provider = "Meshy";
        public string taskId = string.Empty;
        public string sourceHash = string.Empty;
        public string caption = string.Empty;
        public string captionKind = string.Empty;
        public string description = string.Empty;
        public string observedPreviewUrl = string.Empty;
        public string fingerprint = string.Empty;
        public string capturedUtc = string.Empty;
    }

    // Provider text is evidence only. It is never evaluated or used as an instruction.
    internal static class SavicProviderMetadataService
    {
        internal const string Version = "1.0.0";

        internal static bool AttachVerified(SavicStorageLayout layout,
            SavicManifestRepository repository, SavicProviderMetadataInput input)
        {
            if (layout == null || repository == null || input == null)
                throw new ArgumentNullException(nameof(input));
            string downloaded = Path.GetFullPath(input.downloadedOriginalPath);
            if (!File.Exists(downloaded) ||
                !string.Equals(Path.GetExtension(downloaded), ".glb", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Provider metadata requires the downloaded original GLB.");
            string hash = SavicHashService.ComputeSha256(downloaded);
            if (!repository.TryGetBySourceHash(hash, out SavicManifest manifest) ||
                manifest?.source == null || manifest.source.sourceKind != SavicSourceKind.Model3D.ToString() ||
                !string.Equals(manifest.source.sourceHash, hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Downloaded bytes do not identify a canonical SAVIC source.");
            string archive = layout.GetArchivedSourcePath(hash, manifest.source.originalFileName);
            if (layout.ToProjectRelativePath(archive) != manifest.source.archivedRelativePath ||
                !File.Exists(archive) ||
                !string.Equals(SavicHashService.ComputeSha256(archive), hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Canonical original could not be verified before metadata attachment.");
            string zone = SavicDownloadProvenance.Read(downloaded);
            Match hostLine = Regex.Match(zone, @"(?m)^HostUrl=([^\r\n\x00]+)");
            if (!hostLine.Success || !TryResolveMeshyDownloadTask(hostLine.Groups[1].Value,
                    out string downloadTask) ||
                !string.Equals(downloadTask, input.taskId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Provider task does not match the original download provenance.");

            SavicProviderMetadataRecord record = new SavicProviderMetadataRecord
            {
                taskId = downloadTask,
                sourceHash = hash,
                caption = (input.caption ?? string.Empty).Trim(),
                captionKind = input.captionKind,
                description = (input.description ?? string.Empty).Trim(),
                observedPreviewUrl = input.observedPreviewUrl,
                capturedUtc = DateTime.UtcNow.ToString("O")
            };
            if (!HasValidProviderIdentity(record))
                throw new InvalidOperationException("Provider caption or observed task identity is invalid.");
            record.fingerprint = Fingerprint(record);
            if (TryReadVerified(layout, manifest.source, out SavicProviderMetadataRecord existing) &&
                existing.fingerprint == record.fingerprint)
                return false;

            string relative = MetadataPath(record.sourceHash, record.fingerprint);
            string target = layout.FromProjectRelativePath(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            SavicAtomicFile.WriteJson(target, record);
            manifest.source.providerMetadataRelativePath = relative;
            manifest.source.providerMetadataHash = SavicHashService.ComputeSha256(target);
            // Derived classification is now stale; the processing state and output identity stay authoritative.
            manifest.classification ??= new SavicClassificationRecord();
            manifest.classification.classifierVersion = string.Empty;
            SavicManifestMutations.UpsertValidation(manifest, "Source.ProviderMetadata", "PASS", "INFO",
                "Original and download task verified; Meshy " + record.captionKind + " evidence attached for " + record.taskId,
                Version);
            repository.Save(manifest);
            return true;
        }

        internal static string ResolveSemanticName(SavicManifest manifest, SavicStorageLayout layout = null)
        {
            if (manifest?.source == null)
                return string.Empty;
            layout ??= SavicStorageLayout.ForCurrentProject();
            if (SavicAssets4AllService.TryReadVerified(manifest, layout, out _))
                return manifest.assets4All.semanticName;
            if (TryReadVerified(layout, manifest.source, out SavicProviderMetadataRecord metadata))
            {
                // Descriptions can mention excluded objects ("no table", "no wall"). Only the
                // provider's positive subject clause participates in lexical classification.
                int stop = metadata.caption.IndexOfAny(new[] { '.', '\r', '\n', ';' });
                return (stop >= 0 ? metadata.caption.Substring(0, stop) : metadata.caption).Trim();
            }
            return Path.GetFileNameWithoutExtension(manifest.source.originalFileName ?? string.Empty);
        }

        internal static bool TryReadVerified(SavicStorageLayout layout, SavicSourceRecord source,
            out SavicProviderMetadataRecord record)
        {
            record = null;
            if (source == null || !IsHash(source.sourceHash) || !IsHash(source.providerMetadataHash) ||
                string.IsNullOrWhiteSpace(source.providerMetadataRelativePath))
                return false;
            try
            {
                string prefix = "ContentSource/ProviderMetadata/";
                string relative = source.providerMetadataRelativePath;
                if (!relative.StartsWith(prefix, StringComparison.Ordinal) ||
                    relative.Length != prefix.Length + 64 + 5 || !relative.EndsWith(".json", StringComparison.Ordinal) ||
                    !IsHash(relative.Substring(prefix.Length, 64)))
                    return false;
                string path = layout.FromProjectRelativePath(relative);
                if (!File.Exists(path) ||
                    !string.Equals(SavicHashService.ComputeSha256(path), source.providerMetadataHash,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
                SavicProviderMetadataRecord candidate = SavicAtomicFile.ReadJson<SavicProviderMetadataRecord>(path);
                if (!HasValidProviderIdentity(candidate) || candidate.schemaVersion != 1 ||
                    !string.Equals(candidate.sourceHash, source.sourceHash, StringComparison.OrdinalIgnoreCase) ||
                    candidate.fingerprint != Fingerprint(candidate) ||
                    relative != MetadataPath(candidate.sourceHash, candidate.fingerprint))
                    return false;
                record = candidate;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
        }

        internal static bool TryResolveMeshyDownloadTask(string url, out string taskId)
        {
            taskId = string.Empty;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.Host != "assets.meshy.ai")
                return false;
            Match match = Regex.Match(uri.AbsolutePath, @"^/uploads/converted/([0-9a-fA-F-]{36})/[^/]+\.glb$");
            if (!match.Success || !Guid.TryParse(match.Groups[1].Value, out Guid task))
                return false;
            taskId = task.ToString("D");
            return true;
        }

        private static bool HasValidProviderIdentity(SavicProviderMetadataRecord record)
        {
            if (record == null || record.provider != "Meshy" || !Guid.TryParse(record.taskId, out _) ||
                !IsHash(record.sourceHash) || string.IsNullOrWhiteSpace(record.caption) ||
                record.caption.Length > 2000 || record.description == null || record.description.Length > 16000 ||
                (record.captionKind != "MODEL_TITLE" && record.captionKind != "PROMPT_SUBJECT") ||
                !Uri.TryCreate(record.observedPreviewUrl, UriKind.Absolute, out Uri preview) ||
                preview.Scheme != Uri.UriSchemeHttps || preview.Host != "api.meshy.ai" ||
                !string.IsNullOrEmpty(preview.Query) || !string.IsNullOrEmpty(preview.Fragment) ||
                !preview.AbsolutePath.EndsWith("/tasks/" + record.taskId + "/output/preview.png", StringComparison.Ordinal))
                return false;
            if (record.captionKind == "PROMPT_SUBJECT" &&
                !record.description.StartsWith(record.caption, StringComparison.Ordinal))
                return false;
            return true;
        }

        private static string Fingerprint(SavicProviderMetadataRecord record) =>
            SavicHashService.ComputeSha256Text(JsonUtility.ToJson(new SavicProviderMetadataRecord
            {
                taskId = record.taskId, sourceHash = record.sourceHash.ToLowerInvariant(),
                caption = record.caption, captionKind = record.captionKind,
                description = record.description, observedPreviewUrl = record.observedPreviewUrl
            }));

        private static string MetadataPath(string sourceHash, string fingerprint) =>
            "ContentSource/ProviderMetadata/" + fingerprint + ".json";

        private static bool IsHash(string value) => value != null && Regex.IsMatch(value, @"\A[0-9a-fA-F]{64}\z");
    }

    // Unity's Mono path normalization rejects NTFS alternate streams. Open the
    // download provenance through Windows, then use normal managed stream I/O.
    internal static class SavicDownloadProvenance
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access,
            uint share, IntPtr security, uint disposition, uint attributes, IntPtr template);

        internal static FileStream OpenZoneIdentifier(string original, FileAccess access)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                throw new InvalidOperationException("Verified Meshy download provenance requires a Windows original download.");
            string path = Path.GetFullPath(original);
            if (!File.Exists(path) || Path.GetExtension(path).ToLowerInvariant() != ".glb")
                throw new InvalidOperationException("Download provenance requires an existing GLB.");
            uint desiredAccess = access == FileAccess.Read ? 0x80000000u : 0x40000000u;
            SafeFileHandle handle = CreateFileW(path + ":Zone.Identifier", desiredAccess,
                7, IntPtr.Zero, access == FileAccess.Read ? 3u : 2u, 0x80, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new IOException("Original download provenance could not be opened.", new Win32Exception(error));
            }
            try { return new FileStream(handle, access); }
            catch { handle.Dispose(); throw; }
        }

        internal static string Read(string original)
        {
            using (FileStream stream = OpenZoneIdentifier(original, FileAccess.Read))
            {
                if (stream.Length > 32768)
                    throw new InvalidOperationException("Download provenance exceeds the supported size.");
                using (StreamReader reader = new StreamReader(stream)) return reader.ReadToEnd();
            }
        }
    }
}
