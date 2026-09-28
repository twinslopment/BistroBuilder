using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicUnityImageSourceImportAdapter :
        ISavicSourceImportAdapter
    {
        internal const string BuilderId = "savic.source-import.unity-image";
        internal const string BuilderVersion = "1.0.0";

        private readonly SavicStorageLayout layout;

        internal SavicUnityImageSourceImportAdapter(
            SavicStorageLayout layout)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public string AdapterId => BuilderId;
        public string AdapterVersion => BuilderVersion;

        public bool CanImport(SavicManifest manifest)
        {
            if (manifest?.source == null ||
                !string.Equals(
                    manifest.source.sourceKind,
                    SavicSourceKind.Image.ToString(),
                    StringComparison.Ordinal))
            {
                return false;
            }

            string extension =
                manifest.source.extension?.Trim().ToLowerInvariant() ??
                string.Empty;

            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg" ||
                   extension == ".tga" ||
                   extension == ".psd" ||
                   extension == ".tif" ||
                   extension == ".tiff";
        }

        public SavicSourceImportResult Materialize(
            SavicManifest manifest)
        {
            if (!CanImport(manifest))
            {
                return Failure(
                    "No compatible Unity image source import adapter is available.");
            }

            ValidateManifest(manifest);

            string archivedPath =
                layout.FromProjectRelativePath(
                    manifest.source.archivedRelativePath);

            if (!File.Exists(archivedPath))
            {
                return Failure(
                    "Archived image source is missing: " +
                    archivedPath);
            }

            string mirrorAbsolutePath =
                layout.GetUnitySourceMirrorPath(
                    manifest.source.sourceHash,
                    manifest.source.originalFileName);

            string assetPath =
                layout.ToProjectRelativePath(
                    mirrorAbsolutePath);

            try
            {
                bool changed =
                    EnsureMirrorMaterialized(
                        archivedPath,
                        mirrorAbsolutePath,
                        manifest.source.sourceHash);

                return new SavicSourceImportResult(
                    true,
                    changed,
                    assetPath,
                    null,
                    changed
                        ? "Unity image source mirror materialized and hash-verified."
                        : "Existing hash-addressed Unity image source mirror reused.");
            }
            catch (Exception exception)
            {
                return Failure(
                    "Unity image source mirror materialization failed: " +
                    exception.Message,
                    assetPath);
            }
        }

        public SavicSourceImportResult ImportPrepared(
            SavicManifest manifest)
        {
            if (!CanImport(manifest))
            {
                return Failure(
                    "No compatible Unity image source import adapter is available.");
            }

            ValidateManifest(manifest);

            string mirrorAbsolutePath =
                layout.GetUnitySourceMirrorPath(
                    manifest.source.sourceHash,
                    manifest.source.originalFileName);

            string assetPath =
                layout.ToProjectRelativePath(
                    mirrorAbsolutePath);

            if (!File.Exists(mirrorAbsolutePath))
            {
                return Failure(
                    "Prepared Unity image source mirror is missing.",
                    assetPath);
            }

            try
            {
                Texture2D existing =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        assetPath);

                if (existing != null)
                {
                    return new SavicSourceImportResult(
                        true,
                        false,
                        assetPath,
                        existing,
                        "Prepared Unity image source mirror already has a valid Texture2D.");
                }

                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                Texture2D texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        assetPath);

                if (texture == null)
                {
                    return Failure(
                        "Unity imported no Texture2D from the prepared image source.",
                        assetPath,
                        true);
                }

                return new SavicSourceImportResult(
                    true,
                    true,
                    assetPath,
                    texture,
                    "Prepared Unity image source mirror imported successfully.");
            }
            catch (Exception exception)
            {
                return Failure(
                    "Unity image source import failed: " +
                    exception.Message,
                    assetPath,
                    true);
            }
        }

        public SavicSourceImportResult Import(
            SavicManifest manifest)
        {
            SavicSourceImportResult materialized =
                Materialize(manifest);

            if (!materialized.Succeeded)
                return materialized;

            SavicSourceImportResult imported =
                ImportPrepared(manifest);

            if (!imported.Succeeded)
                return imported;

            return new SavicSourceImportResult(
                true,
                materialized.Changed || imported.Changed,
                imported.AssetPath,
                imported.MainObject,
                imported.Message);
        }

        private static void ValidateManifest(
            SavicManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(manifest.savicId))
                throw new InvalidOperationException("Manifest has no SAVIC identity.");
            if (string.IsNullOrWhiteSpace(manifest.source.sourceHash))
                throw new InvalidOperationException("Manifest has no SourceHash.");
            if (string.IsNullOrWhiteSpace(manifest.source.archivedRelativePath))
                throw new InvalidOperationException("Manifest has no archived source path.");
        }

        private static SavicSourceImportResult Failure(
            string message,
            string assetPath = "",
            bool changed = false)
        {
            return new SavicSourceImportResult(
                false,
                changed,
                assetPath,
                null,
                message);
        }

        private static bool EnsureMirrorMaterialized(
            string archivedPath,
            string mirrorAbsolutePath,
            string expectedHash)
        {
            string mirrorDirectory =
                Path.GetDirectoryName(mirrorAbsolutePath)
                ?? throw new InvalidOperationException(
                    "Could not resolve Unity image mirror directory.");

            Directory.CreateDirectory(mirrorDirectory);

            if (File.Exists(mirrorAbsolutePath) &&
                string.Equals(
                    SavicHashService.ComputeSha256(mirrorAbsolutePath),
                    expectedHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string temporaryPath =
                Path.Combine(
                    mirrorDirectory,
                    "." +
                    Path.GetFileName(mirrorAbsolutePath) +
                    ".savic-" +
                    Guid.NewGuid().ToString("N") +
                    ".tmp");

            try
            {
                File.Copy(
                    archivedPath,
                    temporaryPath,
                    false);

                string stagedHash =
                    SavicHashService.ComputeSha256(
                        temporaryPath);

                if (!string.Equals(
                        stagedHash,
                        expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        "Archived image failed SHA-256 integrity validation while materializing the Unity mirror.");
                }

                if (File.Exists(mirrorAbsolutePath))
                    File.Delete(mirrorAbsolutePath);

                File.Move(
                    temporaryPath,
                    mirrorAbsolutePath);

                return true;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
    }
}
