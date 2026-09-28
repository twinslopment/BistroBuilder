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
        internal const string BuilderVersion = "1.1.0";

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

        public SavicSourceImportResult Materialize(
            SavicManifest manifest)
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
                        ? "Unity source mirror materialized and hash-verified."
                        : "Existing hash-addressed Unity source mirror reused.");
            }
            catch (Exception exception)
            {
                return new SavicSourceImportResult(
                    false,
                    false,
                    assetPath,
                    null,
                    "Unity source mirror materialization failed: " +
                    exception.Message);
            }
        }

        public SavicSourceImportResult ImportPrepared(
            SavicManifest manifest)
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

            string mirrorAbsolutePath =
                layout.GetUnitySourceMirrorPath(
                    manifest.source.sourceHash,
                    manifest.source.originalFileName);

            string assetPath =
                layout.ToProjectRelativePath(
                    mirrorAbsolutePath);

            if (!File.Exists(mirrorAbsolutePath))
            {
                return new SavicSourceImportResult(
                    false,
                    false,
                    assetPath,
                    null,
                    "Prepared Unity source mirror is missing.");
            }

            if (!ValidateContainerPreflight(
                    mirrorAbsolutePath,
                    manifest.source.extension,
                    out string preflightError))
            {
                return new SavicSourceImportResult(
                    false,
                    false,
                    assetPath,
                    null,
                    "Prepared Unity source mirror failed preflight: " +
                    preflightError);
            }

            try
            {
                UnityEngine.Object existingMainObject =
                    AssetDatabase.LoadMainAssetAtPath(
                        assetPath);

                if (existingMainObject is GameObject)
                {
                    return new SavicSourceImportResult(
                        true,
                        false,
                        assetPath,
                        existingMainObject,
                        "Prepared Unity source mirror already has a valid imported GameObject.");
                }

                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                UnityEngine.Object mainObject =
                    AssetDatabase.LoadMainAssetAtPath(
                        assetPath);

                if (mainObject == null)
                {
                    return new SavicSourceImportResult(
                        false,
                        true,
                        assetPath,
                        null,
                        "Unity imported no main object from the prepared source.");
                }

                if (mainObject is not GameObject)
                {
                    return new SavicSourceImportResult(
                        false,
                        true,
                        assetPath,
                        mainObject,
                        "Imported 3D source main object is not a GameObject.");
                }

                return new SavicSourceImportResult(
                    true,
                    true,
                    assetPath,
                    mainObject,
                    "Prepared Unity source mirror imported successfully.");
            }
            catch (Exception exception)
            {
                return new SavicSourceImportResult(
                    false,
                    true,
                    assetPath,
                    null,
                    "Unity source import failed: " +
                    exception.Message);
            }
        }

        public SavicSourceImportResult Import(
            SavicManifest manifest)
        {
            SavicSourceImportResult materialized =
                Materialize(
                    manifest);

            if (!materialized.Succeeded)
                return materialized;

            SavicSourceImportResult imported =
                ImportPrepared(
                    manifest);

            if (!imported.Succeeded)
                return imported;

            return new SavicSourceImportResult(
                true,
                materialized.Changed ||
                imported.Changed,
                imported.AssetPath,
                imported.MainObject,
                imported.Message);
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

        private bool EnsureMirrorMaterialized(
            string archivedPath,
            string mirrorAbsolutePath,
            string expectedHash)
        {
            string mirrorDirectory =
                Path.GetDirectoryName(
                    mirrorAbsolutePath)
                ?? throw new InvalidOperationException(
                    "Could not resolve Unity mirror directory.");

            Directory.CreateDirectory(
                mirrorDirectory);

            if (File.Exists(
                    mirrorAbsolutePath))
            {
                string mirrorHash =
                    SavicHashService.ComputeSha256(
                        mirrorAbsolutePath);

                if (string.Equals(
                        mirrorHash,
                        expectedHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            string tempPath =
                Path.Combine(
                    mirrorDirectory,
                    "." +
                    Path.GetFileName(
                        mirrorAbsolutePath) +
                    ".savic-" +
                    Guid.NewGuid().ToString("N") +
                    ".tmp");

            string backupPath =
                mirrorAbsolutePath +
                ".savic-backup";

            try
            {
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

                if (File.Exists(
                        mirrorAbsolutePath))
                {
                    if (File.Exists(
                            backupPath))
                    {
                        File.Delete(
                            backupPath);
                    }

                    File.Replace(
                        tempPath,
                        mirrorAbsolutePath,
                        backupPath,
                        true);

                    if (File.Exists(
                            backupPath))
                    {
                        File.Delete(
                            backupPath);
                    }
                }
                else
                {
                    File.Move(
                        tempPath,
                        mirrorAbsolutePath);
                }

                return true;
            }
            finally
            {
                if (File.Exists(
                        tempPath))
                {
                    File.Delete(
                        tempPath);
                }

                if (File.Exists(
                        backupPath))
                {
                    File.Delete(
                        backupPath);
                }
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
                    buffer,
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

    }
}
