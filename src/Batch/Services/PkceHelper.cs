using System.Security.Cryptography;
using System.Text;

namespace LECG.Batch.Services
{
    internal static class PkceHelper
    {
        public static (string Verifier, string Challenge) Generate()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            string verifier = Base64UrlEncode(bytes);
            byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            string challenge = Base64UrlEncode(hash);
            return (verifier, challenge);
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
    }
}
