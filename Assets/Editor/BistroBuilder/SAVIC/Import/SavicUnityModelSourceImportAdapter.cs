using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicUnityModelSourceImportAdapter :
        ISavicSourceImportAdapter
    {
        internal const string BuilderId = "savic.source-import.unity-model";
        internal const string BuilderVersion = "1.0.0";

        private readonly SavicStorageLayout layout;

        internal SavicUnityModelSourceImportAdapter(
            SavicStorageLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public bool CanImport(SavicManifest manifest)
        {
            if (manifest?.source == null ||
                !string.Equals(
                    manifest.source.sourceKind,
                    SavicSourceKind.Model3D.ToString(),
                    StringComparison.Ordinal))
            {
                return false;
            }

            string extension =
                manifest.source.extension?.Trim().ToLowerInvariant();

            // V1 only imports self-contained/safely-resolvable formats.
            // OBJ/GLTF may depend on sidecar files, so they remain intake-supported
            // but are intentionally blocked here until bundle dependency capture exists.
            return extension == ".glb" ||
                   extension == ".fbx";
        }

        public SavicSourceImportResult Import(SavicManifest manifest)
        {
            if (!CanImport(manifest))
            {
                return new SavicSourceImportResult(
                    false,
                    false,
                    string.Empty,
                    null,
                    "No compatible 3D source import adapter is available.");
            }

            ValidateManifest(manifest);

            string archivedPath =
                layout.FromProjectRelativePath(
                    manifest.source.archivedRelativePath);

            if (!File.Exists(archivedPath))
            {
                return new SavicSourceImportResult(
                    false,
                    false,
                    string.Empty,
                    null,
                    "Archived source is missing: " + archivedPath);
            }

            string archiveHash =
                SavicHashService.ComputeSha256(archivedPath);

            if (!string.Equals(
                    archiveHash,
                    manifest.source.sourceHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new SavicSourceImportResult(
                    false,
                    false,
                    string.Empty,
                    null,
                    "Archived source failed SHA-256 integrity validation.");
            }

            string mirrorAbsolutePath =
                layout.GetUnitySourceMirrorPath(
                    manifest.source.sourceHash,
                    manifest.source.originalFileName);

            string assetPath =
                layout.ToProjectRelativePath(mirrorAbsolutePath);

            MirrorMutation mutation = EnsureMirror(
                archivedPath,
                mirrorAbsolutePath,
                manifest.source.sourceHash);

            bool changed = mutation.Changed;

            try
            {
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                UnityEngine.Object mainObject =
                    AssetDatabase.LoadMainAssetAtPath(assetPath);

                if (mainObject == null)
                {
                    RollbackMirror(assetPath, mutation);
                    return new SavicSourceImportResult(
                        false,
                        changed,
                        assetPath,
                        null,
                        "Unity imported no main object from the source.");
                }

                if (mainObject is not GameObject)
                {
                    RollbackMirror(assetPath, mutation);
                    return new SavicSourceImportResult(
                        false,
                        changed,
                        assetPath,
                        mainObject,
                        "Imported 3D source main object is not a GameObject.");
                }

                mutation.Commit();

                return new SavicSourceImportResult(
                    true,
                    changed,
                    assetPath,
                    mainObject,
                    changed
                        ? "Unity source mirror materialized and imported."
                        : "Existing Unity source mirror validated and imported.");
            }
            catch (Exception exception)
            {
                RollbackMirror(assetPath, mutation);
                return new SavicSourceImportResult(
                    false,
                    changed,
                    assetPath,
                    null,
                    "Unity source import failed: " + exception.Message);
            }
        }

        private static void ValidateManifest(SavicManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(manifest.savicId))
                throw new InvalidOperationException("Manifest has no SAVIC identity.");
            if (string.IsNullOrWhiteSpace(manifest.source.sourceHash))
                throw new InvalidOperationException("Manifest has no SourceHash.");
            if (string.IsNullOrWhiteSpace(manifest.source.archivedRelativePath))
                throw new InvalidOperationException("Manifest has no archived source path.");
        }

        private MirrorMutation EnsureMirror(
            string archivedPath,
            string mirrorAbsolutePath,
            string expectedHash)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(mirrorAbsolutePath)
                ?? throw new InvalidOperationException(
                    "Could not resolve Unity mirror directory."));

            bool hadExisting = File.Exists(mirrorAbsolutePath);

            if (hadExisting)
            {
                string mirrorHash =
                    SavicHashService.ComputeSha256(mirrorAbsolutePath);

                if (string.Equals(
                        mirrorHash,
                        expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return MirrorMutation.Unchanged(mirrorAbsolutePath);
                }
            }

            Directory.CreateDirectory(layout.StagingRoot);

            string tempPath = Path.Combine(
                layout.StagingRoot,
                "mirror_" + Guid.NewGuid().ToString("N") +
                Path.GetExtension(mirrorAbsolutePath));

            string backupPath = hadExisting
                ? Path.Combine(
                    layout.StagingRoot,
                    "mirror_backup_" + Guid.NewGuid().ToString("N") +
                    Path.GetExtension(mirrorAbsolutePath))
                : string.Empty;

            try
            {
                if (hadExisting)
                    File.Copy(mirrorAbsolutePath, backupPath, true);

                File.Copy(archivedPath, tempPath, true);

                string tempHash =
                    SavicHashService.ComputeSha256(tempPath);

                if (!string.Equals(
                        tempHash,
                        expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        "Staged Unity source mirror failed SHA-256 verification.");
                }

                File.Copy(tempPath, mirrorAbsolutePath, true);

                return new MirrorMutation(
                    mirrorAbsolutePath,
                    backupPath,
                    hadExisting,
                    true);
            }
            catch
            {
                if (hadExisting && File.Exists(backupPath))
                    File.Copy(backupPath, mirrorAbsolutePath, true);
                else if (!hadExisting && File.Exists(mirrorAbsolutePath))
                    File.Delete(mirrorAbsolutePath);

                throw;
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        private static void RollbackMirror(
            string assetPath,
            MirrorMutation mutation)
        {
            if (mutation == null || !mutation.Changed)
                return;

            try
            {
                mutation.Rollback();

                if (mutation.HadExisting)
                {
                    AssetDatabase.ImportAsset(
                        assetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                }
                else
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
            catch (Exception cleanupException)
            {
                Debug.LogError(
                    "[SAVIC] Source mirror rollback failed for '" +
                    assetPath + "': " + cleanupException);
            }
        }

        private sealed class MirrorMutation
        {
            internal MirrorMutation(
                string mirrorPath,
                string backupPath,
                bool hadExisting,
                bool changed)
            {
                MirrorPath = mirrorPath;
                BackupPath = backupPath;
                HadExisting = hadExisting;
                Changed = changed;
            }

            internal string MirrorPath { get; }
            internal string BackupPath { get; }
            internal bool HadExisting { get; }
            internal bool Changed { get; }

            internal static MirrorMutation Unchanged(string mirrorPath)
            {
                return new MirrorMutation(
                    mirrorPath,
                    string.Empty,
                    true,
                    false);
            }

            internal void Commit()
            {
                if (!string.IsNullOrWhiteSpace(BackupPath) &&
                    File.Exists(BackupPath))
                {
                    File.Delete(BackupPath);
                }
            }

            internal void Rollback()
            {
                if (!Changed)
                    return;

                if (HadExisting)
                {
                    if (!File.Exists(BackupPath))
                    {
                        throw new IOException(
                            "SAVIC mirror rollback backup is missing.");
                    }

                    File.Copy(BackupPath, MirrorPath, true);
                }
                else if (File.Exists(MirrorPath))
                {
                    File.Delete(MirrorPath);
                }

                if (!string.IsNullOrWhiteSpace(BackupPath) &&
                    File.Exists(BackupPath))
                {
                    File.Delete(BackupPath);
                }
            }
        }

    }
}
