using System;
using System.IO;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    // A legacy project's source folder can supply context only when its bytes
    // match the canonical archived original. Generated mirrors are excluded.
    internal static class SavicVerifiedProjectSourceContext
    {
        internal static bool TryResolve(
            SavicSourceRecord source,
            out string folderName,
            out string projectRelativePath)
        {
            folderName = string.Empty;
            projectRelativePath = string.Empty;
            if (source == null)
                return false;

            string assetsRoot = Application.dataPath;
            return TryResolveUnderRoot(
                source,
                Path.Combine(assetsRoot, "Assetsparajuego"),
                "Assets/Assetsparajuego",
                out folderName,
                out projectRelativePath);
        }

        internal static bool TryResolveUnderRoot(
            SavicSourceRecord source,
            string root,
            string relativeRoot,
            out string folderName,
            out string projectRelativePath)
        {
            folderName = string.Empty;
            projectRelativePath = string.Empty;
            if (source == null ||
                string.IsNullOrWhiteSpace(root) ||
                string.IsNullOrWhiteSpace(relativeRoot) ||
                !Directory.Exists(root) ||
                !IsSha256(source.sourceHash))
                return false;

            string fileName = source.originalFileName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(fileName, Path.GetFileName(fileName),
                    StringComparison.Ordinal))
                return false;

            string resolvedFolder = string.Empty;
            string resolvedPath = string.Empty;
            try
            {
                foreach (string candidate in Directory.EnumerateFiles(
                             root, fileName, SearchOption.AllDirectories))
                {
                    if (!string.Equals(
                            SavicHashService.ComputeSha256(candidate),
                            source.sourceHash,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    string candidateFolder = Path.GetFileName(
                        Path.GetDirectoryName(candidate));
                    if (string.IsNullOrWhiteSpace(candidateFolder))
                        continue;

                    // Conflicting source folders cannot decide semantics.
                    if (resolvedFolder.Length > 0 &&
                        !string.Equals(resolvedFolder, candidateFolder,
                            StringComparison.OrdinalIgnoreCase))
                        return false;

                    resolvedFolder = candidateFolder;
                    resolvedPath = relativeRoot.TrimEnd('/') + "/" +
                        candidate.Substring(
                            Path.GetFullPath(root).TrimEnd(
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar).Length + 1)
                            .Replace('\\', '/');
                }
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }

            if (resolvedFolder.Length == 0)
                return false;

            folderName = resolvedFolder;
            projectRelativePath = resolvedPath;
            return true;
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
                return false;
            foreach (char character in value)
                if (!Uri.IsHexDigit(character))
                    return false;
            return true;
        }
    }
}
