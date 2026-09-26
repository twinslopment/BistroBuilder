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

            if (!ValidateContainerPreflight(
                    archivedPath,
                    manifest.source.extension,
                    out string preflightError))
            {
                return new SavicSourceImportResult(
                    false,
                    false,
                    string.Empty,
                    null,
                    "3D source preflight rejected the archived file before Unity import: " +
                    preflightError);
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
                if (!changed)
                {
                    UnityEngine.Object existingMainObject =
                        AssetDatabase.LoadMainAssetAtPath(
                            assetPath);

                    if (existingMainObject is GameObject)
                    {
                        mutation.Commit();

                        return new SavicSourceImportResult(
                            true,
                            false,
                            assetPath,
                            existingMainObject,
                            "Existing validated Unity source mirror reused without reimport.");
                    }
                }

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

        private static bool ValidateContainerPreflight(
            string archivedPath,
            string extension,
            out string error)
        {
            error = string.Empty;

            string normalized =
                extension?.Trim().ToLowerInvariant() ??
                string.Empty;

            if (normalized != ".glb")
                return true;

            FileInfo info =
                new FileInfo(
                    archivedPath);

            if (!info.Exists ||
                info.Length < 12)
            {
                error =
                    "GLB header is missing or truncated.";
                return false;
            }

            byte[] header =
                new byte[12];

            using (FileStream stream =
                   File.Open(
                       archivedPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                if (stream.Read(
                        header,
                        0,
                        header.Length) !=
                    header.Length)
                {
                    error =
                        "GLB header could not be read completely.";
                    return false;
                }
            }

            uint magic =
                BitConverter.ToUInt32(
                    header,
                    0);

            uint version =
                BitConverter.ToUInt32(
                    header,
                    4);

            uint declaredLength =
                BitConverter.ToUInt32(
                    header,
                    8);

            if (magic != 0x46546C67u)
            {
                error =
                    "GLB magic is invalid.";
                return false;
            }

            if (version != 2u)
            {
                error =
                    "Only GLB version 2 is accepted by SAVIC V1.";
                return false;
            }

            if (declaredLength !=
                (uint)info.Length)
            {
                error =
                    "GLB declared length does not match archived byte length.";
                return false;
            }

            return true;
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

                string tempHash =
                    CopyFileAndComputeSha256(
                        archivedPath,
                        tempPath);

                if (!string.Equals(
                        tempHash,
                        expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        "Archived source failed SHA-256 integrity validation while materializing the Unity mirror.");
                }

                if (hadExisting)
                {
                    File.Copy(
                        tempPath,
                        mirrorAbsolutePath,
                        true);
                }
                else
                {
                    File.Move(
                        tempPath,
                        mirrorAbsolutePath);
                }

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

        private static string CopyFileAndComputeSha256(
            string sourcePath,
            string destinationPath)
        {
            const int bufferSize =
                1024 * 1024;

            byte[] buffer =
                new byte[bufferSize];

            using System.Security.Cryptography.SHA256 sha =
                System.Security.Cryptography.SHA256.Create();

            using FileStream source =
                new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize,
                    FileOptions.SequentialScan);

            using FileStream destination =
                new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize,
                    FileOptions.SequentialScan);

            int read;

            while ((read =
                        source.Read(
                            buffer,
                            0,
                            buffer.Length)) > 0)
            {
                sha.TransformBlock(
                    buffer,
                    0,
                    read,
                    null,
                    0);

                destination.Write(
                    buffer,
                    0,
                    read);
            }

            sha.TransformFinalBlock(
                Array.Empty<byte>(),
                0,
                0);

            byte[] hash =
                sha.Hash ??
                throw new IOException(
                    "SHA-256 did not produce a mirror hash.");

            return string.Concat(
                Array.ConvertAll(
                    hash,
                    value =>
                        value.ToString("x2")));
        }

        private static void RollbackMirror(
            string assetPath,
            MirrorMutation mutation)
        {
            if (mutation == null || !mutation.Changed)
                return;

            try
            {
                if (!mutation.HadExisting)
                {
                    // Let Unity remove both the asset and its .meta before
                    // deleting any raw mirror bytes. This avoids orphaned
                    // metadata after a failed importer.
                    AssetDatabase.DeleteAsset(
                        assetPath);

                    mutation.Rollback();
                    return;
                }

                mutation.Rollback();

                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
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
