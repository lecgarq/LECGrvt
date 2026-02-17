using System.IO;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using LECG.Services.Logging;

namespace LECG.Services
{
    public class FamilySaveService : IFamilySaveService
    {
        public string SaveTemp(Document targetFamilyDoc, string targetFamilyName)
        {
            string tempDir = Path.GetTempPath();
            string tempFamilyPath = Path.Combine(tempDir, targetFamilyName + ".rfa");

            if (File.Exists(tempFamilyPath)) File.Delete(tempFamilyPath);

            SaveAsOptions saveOpts = new SaveAsOptions() { OverwriteExistingFile = true };
            targetFamilyDoc.SaveAs(tempFamilyPath, saveOpts);
            Logger.Instance.Log($"Saved temporary family to: {tempFamilyPath}");

            return tempFamilyPath;
        }
    }
}
