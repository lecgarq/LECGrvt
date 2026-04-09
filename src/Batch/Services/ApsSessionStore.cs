using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LECG.Batch.Configuration;
using LECG.Batch.Models;
using LECG.Batch.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Batch.Services
{
    public class ApsSessionStore : IApsSessionStore
    {
        public ApsSession? Load()
        {
            string path = BatchConstants.SessionPath;
            if (!File.Exists(path)) return null;

            try
            {
                byte[] encrypted = File.ReadAllBytes(path);
                byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(decrypted);
                return JsonSerializer.Deserialize<ApsSession>(json);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning($"[APS] Failed to load session: {ex.Message}");
                return null;
            }
        }

        public void Save(ApsSession session)
        {
            string path = BatchConstants.SessionPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string json = JsonSerializer.Serialize(session);
            byte[] plaintext = Encoding.UTF8.GetBytes(json);
            byte[] encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }

        public void Delete()
        {
            string path = BatchConstants.SessionPath;
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
