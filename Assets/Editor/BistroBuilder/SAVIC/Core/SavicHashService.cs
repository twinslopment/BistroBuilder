using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicHashService
    {
        private const int BufferSize = 1024 * 1024;

        internal static string ComputeSha256(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A source path is required.", nameof(path));

            using FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BufferSize,
                FileOptions.SequentialScan);

            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);

            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int index = 0; index < hash.Length; index++)
                builder.Append(hash[index].ToString("x2"));

            return builder.ToString();
        }

        internal static bool CanAcquireExclusiveRead(string path)
        {
            try
            {
                using FileStream _ = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None,
                    1,
                    FileOptions.None);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
