using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicAtomicFile
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        internal static void WriteJson<T>(string path, T value)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A target path is required.", nameof(path));

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Could not resolve target directory.");

            Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(value, true) + Environment.NewLine;
            string tempPath = path + ".tmp." + Guid.NewGuid().ToString("N");
            string backupPath = path + ".bak";

            try
            {
                File.WriteAllText(tempPath, json, Utf8NoBom);

                if (!File.Exists(path))
                {
                    File.Move(tempPath, path);
                    return;
                }

                try
                {
                    File.Replace(tempPath, path, backupPath, true);
                    TryDelete(backupPath);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceFallback(tempPath, path);
                }
                catch (IOException)
                {
                    ReplaceFallback(tempPath, path);
                }
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        internal static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path, Utf8NoBom);
            return JsonUtility.FromJson<T>(json);
        }

        private static void ReplaceFallback(string tempPath, string path)
        {
            string fallbackBackup = path + ".fallback.bak";
            TryDelete(fallbackBackup);

            File.Copy(path, fallbackBackup, true);
            try
            {
                File.Copy(tempPath, path, true);
                TryDelete(fallbackBackup);
            }
            catch
            {
                if (File.Exists(fallbackBackup))
                    File.Copy(fallbackBackup, path, true);
                throw;
            }
            finally
            {
                TryDelete(fallbackBackup);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Cleanup failure must not hide the original operation result.
            }
        }
    }
}
