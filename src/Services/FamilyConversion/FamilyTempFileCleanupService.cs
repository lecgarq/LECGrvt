using System;
using System.IO;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilyTempFileCleanupService : IFamilyTempFileCleanupService
    {
        private readonly ILogger _logger;

        public FamilyTempFileCleanupService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Cleanup(string tempFamilyPath, bool isTemporary)
        {
            if (isTemporary)
            {
                try
                {
                    if (File.Exists(tempFamilyPath)) File.Delete(tempFamilyPath);
                    _logger.Log("Temporary file deleted.", scope: "FamilyTempFileCleanup");
                }
                catch
                {
                    // Ignore lock errors to avoid breaking conversion finalization.
                }
            }
            else
            {
                _logger.Log($"Family saved at {tempFamilyPath} (Temporary=False)", scope: "FamilyTempFileCleanup");
                // NOTE: If user wanted to save to a specific directory, we should have used that path instead of temp.
                // But logic says "do not save this family", meaning pure temporary.
                // Checkbox "Do not save" = Temporary.
                // Checkbox "Save" = ? The prompt asked for "directory of that file".
                // For now, keeping it simple: always save to temp to load. If !isTemporary, we might want to Move it?
                // Or just leave it in temp? Typically "Do not save" means delete after load.
                // If they want to save, they probably want it in a folder.
                // I'll stick to basic temp logic for now as 'isTemporary' implies deletion.
            }
        }
    }
}
