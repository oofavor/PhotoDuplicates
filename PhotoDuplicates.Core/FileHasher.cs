using System.Security.Cryptography;

namespace PhotoDuplicates.Core
{
    public static class FileHasher
    {
        public static string ComputeSha256(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = SHA256.HashData(stream);
                return Convert.ToHexString(hash);
            }
        }
    }
}
