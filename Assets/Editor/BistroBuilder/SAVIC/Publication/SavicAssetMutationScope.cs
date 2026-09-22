using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicAssetMutationScope : IDisposable
    {
        private sealed class Snapshot
        {
            internal string AssetPath;
            internal string AbsolutePath;
            internal string BackupPath;
            internal string MetaPath;
            internal string MetaBackupPath;
            internal bool AssetExisted;
            internal bool MetaExisted;
        }

        private readonly List<Snapshot> snapshots =
            new List<Snapshot>();

        private readonly string backupRoot;
        private bool completed;
        private bool disposed;

        internal SavicAssetMutationScope(
            SavicStorageLayout layout,
            string operationName)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            string safeOperation =
                SanitizeFileName(
                    string.IsNullOrWhiteSpace(operationName)
                        ? "mutation"
                        : operationName);

            backupRoot = Path.Combine(
                layout.CacheRoot,
                "Transactions",
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") +
                "_" +
                safeOperation +
                "_" +
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(backupRoot);
        }

        internal void CaptureAsset(string assetPath)
        {
            ThrowIfCompleted();

            string normalized =
                NormalizeAssetPath(assetPath);

            if (FindSnapshot(normalized) != null)
                return;

            string absolute =
                ToAbsoluteProjectPath(normalized);

            string meta =
                absolute + ".meta";

            Snapshot snapshot =
                new Snapshot
                {
                    AssetPath = normalized,
                    AbsolutePath = absolute,
                    MetaPath = meta,
                    AssetExisted = File.Exists(absolute),
                    MetaExisted = File.Exists(meta)
                };

            string token =
                snapshots.Count.ToString("D4");

            if (snapshot.AssetExisted)
            {
                snapshot.BackupPath =
                    Path.Combine(
                        backupRoot,
                        token + ".asset.bak");

                File.Copy(
                    absolute,
                    snapshot.BackupPath,
                    true);
            }

            if (snapshot.MetaExisted)
            {
                snapshot.MetaBackupPath =
                    Path.Combine(
                        backupRoot,
                        token + ".meta.bak");

                File.Copy(
                    meta,
                    snapshot.MetaBackupPath,
                    true);
            }

            snapshots.Add(snapshot);
        }

        internal void Commit()
        {
            ThrowIfCompleted();
            completed = true;
            DeleteBackupRoot();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            if (completed)
                return;

            try
            {
                Rollback();
            }
            finally
            {
                completed = true;
                DeleteBackupRoot();
            }
        }

        private void Rollback()
        {
            for (int index = snapshots.Count - 1;
                 index >= 0;
                 index--)
            {
                Snapshot snapshot =
                    snapshots[index];

                RestoreFile(
                    snapshot.AssetExisted,
                    snapshot.BackupPath,
                    snapshot.AbsolutePath);

                RestoreFile(
                    snapshot.MetaExisted,
                    snapshot.MetaBackupPath,
                    snapshot.MetaPath);
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static void RestoreFile(
            bool originallyExisted,
            string backupPath,
            string destinationPath)
        {
            if (originallyExisted)
            {
                if (string.IsNullOrWhiteSpace(backupPath) ||
                    !File.Exists(backupPath))
                {
                    throw new IOException(
                        "SAVIC rollback backup is missing for: " +
                        destinationPath);
                }

                string directory =
                    Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.Copy(
                    backupPath,
                    destinationPath,
                    true);

                return;
            }

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
        }

        private Snapshot FindSnapshot(string assetPath)
        {
            for (int index = 0;
                 index < snapshots.Count;
                 index++)
            {
                Snapshot snapshot =
                    snapshots[index];

                if (string.Equals(
                        snapshot.AssetPath,
                        assetPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return snapshot;
                }
            }

            return null;
        }

        private static string NormalizeAssetPath(
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException(
                    "Asset path is empty.",
                    nameof(assetPath));

            string normalized =
                assetPath.Replace('\\', '/').Trim();

            if (!normalized.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    normalized,
                    "Assets",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "SAVIC mutation scope only accepts project Assets paths.");
            }

            if (normalized.Contains("../", StringComparison.Ordinal) ||
                normalized.Contains("/..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Asset path traversal is not allowed.");
            }

            return normalized;
        }

        private static string ToAbsoluteProjectPath(
            string assetPath)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException(
                    "Unity project root could not be resolved.");

            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private void ThrowIfCompleted()
        {
            if (completed || disposed)
            {
                throw new ObjectDisposedException(
                    nameof(SavicAssetMutationScope));
            }
        }

        private void DeleteBackupRoot()
        {
            try
            {
                if (Directory.Exists(backupRoot))
                    Directory.Delete(backupRoot, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[SAVIC] Could not delete transaction backup: " +
                    exception.Message);
            }
        }

        private static string SanitizeFileName(string raw)
        {
            char[] invalid =
                Path.GetInvalidFileNameChars();

            string result = raw;

            for (int index = 0;
                 index < invalid.Length;
                 index++)
            {
                result =
                    result.Replace(
                        invalid[index],
                        '_');
            }

            return result;
        }
    }
}
