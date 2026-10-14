using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicUnityStructuredDataSourceImportAdapter :
        ISavicSourceImportAdapter
    {
        internal const string BuilderId =
            "savic.source-import.unity-structured-data";
        internal const string BuilderVersion = "1.0.0";

        private readonly SavicStorageLayout layout;

        internal SavicUnityStructuredDataSourceImportAdapter(
            SavicStorageLayout layout)
        {
            this.layout =
                layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public string AdapterId => BuilderId;
        public string AdapterVersion => BuilderVersion;

        public bool CanImport(SavicManifest manifest)
        {
            return manifest?.source != null &&
                   string.Equals(
                       manifest.source.sourceKind,
                       SavicSourceKind.StructuredData.ToString(),
                       StringComparison.Ordinal) &&
                   string.Equals(
                       manifest.source.extension,
                       ".json",
                       StringComparison.OrdinalIgnoreCase);
        }

        public SavicSourceImportResult Materialize(
            SavicManifest manifest)
        {
            if (!CanImport(manifest))
            {
                return Failure(
                    "No compatible structured-data adapter is available.");
            }

            ValidateManifest(manifest);

            string archivedPath =
                layout.FromProjectRelativePath(
                    manifest.source.archivedRelativePath);

            if (!File.Exists(archivedPath))
            {
                return Failure(
                    "Archived structured-data source is missing: " +
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
                        ? "Structured-data SourceMirror materialized and hash-verified."
                        : "Existing structured-data SourceMirror reused.");
            }
            catch (Exception exception)
            {
                return Failure(
                    "Structured-data materialization failed: " +
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
                    "No compatible structured-data adapter is available.");
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
                    "Prepared structured-data SourceMirror is missing.",
                    assetPath);
            }

            try
            {
                TextAsset existing =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(
                        assetPath);

                if (existing != null)
                {
                    return new SavicSourceImportResult(
                        true,
                        false,
                        assetPath,
                        existing,
                        "Prepared structured-data SourceMirror already resolves as TextAsset.");
                }

                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                TextAsset text =
                    AssetDatabase.LoadAssetAtPath<TextAsset>(
                        assetPath);

                if (text == null)
                {
                    return Failure(
                        "Unity imported no TextAsset from structured data.",
                        assetPath,
                        true);
                }

                return new SavicSourceImportResult(
                    true,
                    true,
                    assetPath,
                    text,
                    "Structured-data source imported successfully.");
            }
            catch (Exception exception)
            {
                return Failure(
                    "Structured-data import failed: " +
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
                throw new InvalidOperationException(
                    "Manifest has no SAVIC identity.");

            if (string.IsNullOrWhiteSpace(
                    manifest.source.sourceHash))
            {
                throw new InvalidOperationException(
                    "Manifest has no SourceHash.");
            }

            if (string.IsNullOrWhiteSpace(
                    manifest.source.archivedRelativePath))
            {
                throw new InvalidOperationException(
                    "Manifest has no archived source path.");
            }
        }

        private static bool EnsureMirrorMaterialized(
            string archivedPath,
            string mirrorAbsolutePath,
            string expectedHash)
        {
            string mirrorDirectory =
                Path.GetDirectoryName(mirrorAbsolutePath)
                ?? throw new InvalidOperationException(
                    "Could not resolve structured-data mirror directory.");

            Directory.CreateDirectory(mirrorDirectory);

            if (File.Exists(mirrorAbsolutePath) &&
                string.Equals(
                    SavicHashService.ComputeSha256(
                        mirrorAbsolutePath),
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
                        "Structured-data SourceMirror failed SHA-256 validation.");
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
    }
}
