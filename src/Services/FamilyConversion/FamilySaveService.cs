using System;
using System.IO;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilySaveService : IFamilySaveService
    {
        private readonly ILogger _logger;

        public FamilySaveService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string SaveTemp(Document targetFamilyDoc, string targetFamilyName)
        {
            ArgumentNullException.ThrowIfNull(targetFamilyDoc);
            ArgumentNullException.ThrowIfNull(targetFamilyName);

            string tempDir = Path.GetTempPath();
            string tempFamilyPath = Path.Combine(tempDir, targetFamilyName + ".rfa");

            if (File.Exists(tempFamilyPath)) File.Delete(tempFamilyPath);

            SaveAsOptions saveOpts = new SaveAsOptions() { OverwriteExistingFile = true };
            targetFamilyDoc.SaveAs(tempFamilyPath, saveOpts);
            _logger.Log($"Saved temporary family to: {tempFamilyPath}", scope: "FamilySave");

            return tempFamilyPath;
        }
    }
}
