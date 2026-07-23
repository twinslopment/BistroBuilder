using System;
using UnityEditor;

namespace BistroBuilder.AssetStudioBB.Editor
{
    internal static class AssetStudioBBPaths
    {
        private const string ManagedMarker = "/Art/AssetStudioBB/";
        private const string ManifestSuffix = ".bbstudio.json";

        public static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/');

        public static bool IsManagedModel(string path)
        {
            var normalized = Normalize(path);
            return normalized.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                && normalized.IndexOf(ManagedMarker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsManagedTexture(string path)
        {
            var normalized = Normalize(path);
            return normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && normalized.IndexOf(ManagedMarker, StringComparison.OrdinalIgnoreCase) >= 0
                && normalized.IndexOf("/Textures/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsManifest(string path)
        {
            return Normalize(path).EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase);
        }

        public static string Directory(string path)
        {
            var normalized = Normalize(path).TrimEnd('/');
            var index = normalized.LastIndexOf('/');
            return index > 0 ? normalized.Substring(0, index) : string.Empty;
        }

        public static string FileName(string path)
        {
            var normalized = Normalize(path);
            var index = normalized.LastIndexOf('/');
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }

        public static string FileNameWithoutExtension(string path)
        {
            var name = FileName(path);
            var index = name.LastIndexOf('.');
            return index > 0 ? name.Substring(0, index) : name;
        }

        public static string Combine(string left, string right)
        {
            left = Normalize(left).TrimEnd('/');
            right = Normalize(right).TrimStart('/');
            return string.IsNullOrEmpty(left) ? right : $"{left}/{right}";
        }

        public static string AssetRootFromManifest(string manifestPath) => Directory(manifestPath);

        public static string AbsoluteAssetPath(string manifestPath, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative))
                return string.Empty;
            if (relative.StartsWith("Assets/", StringComparison.Ordinal))
                return Normalize(relative);
            return Combine(AssetRootFromManifest(manifestPath), relative);
        }

        public static string FindManifestFromSelection(string selectedPath)
        {
            selectedPath = Normalize(selectedPath);
            if (IsManifest(selectedPath))
                return selectedPath;
            if (!IsManagedModel(selectedPath))
                return string.Empty;

            var modelsFolder = Directory(selectedPath);
            var assetRoot = Directory(modelsFolder);
            var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { assetRoot });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsManifest(path))
                    return path;
            }
            return string.Empty;
        }

        public static void EnsureFolder(string folder)
        {
            folder = Normalize(folder).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
                return;
            var parts = folder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
