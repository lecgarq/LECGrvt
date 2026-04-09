using System;
using System.IO;

namespace LECG.Batch.Configuration
{
    public static class BatchConstants
    {
        public static string BatchDir      => Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "LECG", "Batch");
        public static string ManifestPath  => Path.Combine(BatchDir, "manifest.json");
        public static string ReportsDir    => Path.Combine(BatchDir, "Reports");
        public static string AuthDir       => Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "LECG", "Auth");
        public static string SessionPath   => Path.Combine(AuthDir, "session.json");

        public const int DefaultMaxRetries      = 2;
        public const int SyncRetryDelayMs       = 30_000;
        public const int TokenRefreshBufferMin  = 5;
        public const string ApsBaseUrl          = "https://developer.api.autodesk.com";
    }
}
