using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicStorageLayout
    {
        internal SavicStorageLayout(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));

            ProjectRoot = Path.GetFullPath(projectRoot);
        }

        internal string ProjectRoot { get; }
        internal string InboxRoot => Path.Combine(ProjectRoot, "ContentInbox");
        internal string DropHereRoot => Path.Combine(InboxRoot, "DropHere");
        internal string ContentSourceRoot => Path.Combine(ProjectRoot, "ContentSource");
        internal string SavicRoot => Path.Combine(ProjectRoot, "SAVIC");
        internal string ManifestsRoot => Path.Combine(SavicRoot, "Manifests");
        internal string RuntimeRoot => Path.Combine(ProjectRoot, "Library", "BistroBuilder", "SAVIC");
        internal string JobsRoot => Path.Combine(RuntimeRoot, "Jobs");
        internal string LogsRoot => Path.Combine(RuntimeRoot, "Logs");
        internal string CacheRoot => Path.Combine(RuntimeRoot, "Cache");
        internal string StagingRoot => Path.Combine(RuntimeRoot, "Staging");
        internal string QueueSnapshotPath => Path.Combine(JobsRoot, "queue.json");
        internal string ProjectInventorySnapshotPath => Path.Combine(
            CacheRoot,
            "Inventory",
            "project-inventory.json");
        internal string UnitySourceMirrorRoot => Path.Combine(
            ProjectRoot,
            "Assets",
            "Generated",
            "BistroBuilder",
            "SAVIC",
            "SourceMirror");

        internal static SavicStorageLayout ForCurrentProject()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new InvalidOperationException("Could not resolve the Unity project root.");

            return new SavicStorageLayout(projectRoot);
        }

        internal void EnsureInfrastructure()
        {
            Directory.CreateDirectory(DropHereRoot);
            Directory.CreateDirectory(ContentSourceRoot);
            Directory.CreateDirectory(ManifestsRoot);
            Directory.CreateDirectory(JobsRoot);
            Directory.CreateDirectory(LogsRoot);
            Directory.CreateDirectory(CacheRoot);
            Directory.CreateDirectory(StagingRoot);
        }

        internal string GetUnitySourceMirrorPath(
            string sourceHash,
            string originalFileName)
        {
            ValidateSha256(sourceHash);
            string safeName = SanitizeFileName(originalFileName);
            return Path.Combine(
                UnitySourceMirrorRoot,
                sourceHash.Substring(0, 2),
                sourceHash,
                safeName);
        }

        internal string GetArchivedSourcePath(string sourceHash, string originalFileName)
        {
            ValidateSha256(sourceHash);
            string safeName = SanitizeFileName(originalFileName);
            return Path.Combine(
                ContentSourceRoot,
                "SHA256",
                sourceHash.Substring(0, 2),
                sourceHash,
                safeName);
        }

        internal string ToProjectRelativePath(string absolutePath)
        {
            string fullPath = Path.GetFullPath(absolutePath);
            string rootWithSeparator = ProjectRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path is outside the project root: " + fullPath);

            return fullPath.Substring(rootWithSeparator.Length).Replace('\\', '/');
        }

        internal string FromProjectRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            return Path.GetFullPath(Path.Combine(
                ProjectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string SanitizeFileName(string fileName)
        {
            string candidate = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(candidate))
                candidate = "source.bin";

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = candidate
                .Select(c => invalid.Contains(c) ? '_' : c)
                .ToArray();

            return new string(chars);
        }

        private static void ValidateSha256(string sourceHash)
        {
            if (string.IsNullOrWhiteSpace(sourceHash) || sourceHash.Length != 64 ||
                sourceHash.Any(c => !Uri.IsHexDigit(c)))
            {
                throw new ArgumentException("Expected a 64-character SHA-256 hash.", nameof(sourceHash));
            }
        }
    }
}
