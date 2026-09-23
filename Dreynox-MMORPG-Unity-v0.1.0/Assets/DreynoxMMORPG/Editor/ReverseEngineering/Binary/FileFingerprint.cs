using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Binary
{
    public static class FileFingerprint
    {
        public static string Sha256(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA256 sha = SHA256.Create())
                return ToHex(sha.ComputeHash(stream));
        }
        public static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create()) return ToHex(sha.ComputeHash(bytes));
        }
        public static string ToHex(byte[] bytes)
        {
            StringBuilder b = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) b.Append(bytes[i].ToString("x2"));
            return b.ToString();
        }
    }
}
