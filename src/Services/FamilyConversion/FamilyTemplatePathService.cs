using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;
using System.IO;
using System.Linq;

namespace LECG.Services
{
    public class FamilyTemplatePathService : IFamilyTemplatePathService
    {
        public string GetTargetTemplatePath(Application app, Category category)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(category);

            string appVersion = app.VersionNumber;
            string templateName = "";

            if (category.Id.Value == (long)BuiltInCategory.OST_Doors)
                templateName = "LECG_047_DOORS.rft";
            else if (category.Id.Value == (long)BuiltInCategory.OST_Windows)
                templateName = "LECG_179_WINDOWS.rft";
            else if (category.Id.Value == (long)BuiltInCategory.OST_Furniture)
                templateName = "Metric Generic Model.rft";

            // 1. Try LECG Specific
            if (!string.IsNullOrEmpty(templateName) && !templateName.Contains("Generic"))
            {
                string customPath = $@"C:\ProgramData\Autodesk\RVT {appVersion}\Family Templates\English\LECG\-\{templateName}";
                if (File.Exists(customPath)) return customPath;
            }

            // 2. Try Standard Revit Paths
            string rootPath = app.FamilyTemplatePath;
            string[] possibleFiles = new[] { "Metric Generic Model.rft", "Generic Model.rft" };

            // Check in root
            if (Directory.Exists(rootPath))
            {
                foreach (var name in possibleFiles)
                {
                    string fullPath = Path.Combine(rootPath, name);
                    if (File.Exists(fullPath)) return fullPath;
                }

                // Check in subfolders (recursive search)
                try
                {
                    var files = Directory.GetFiles(rootPath, "*Generic Model.rft", SearchOption.AllDirectories);
                    if (files.Any()) return files.First();
                }
                catch (Exception ex) when (IsExpectedTemplateSearchException(ex))
                {
                    Logging.Logger.Instance.LogWarning($"[FamilyTemplatePathService] Generic template search failed in {rootPath}: {ex.Message}");
                }
            }

            // 3. Try Hardcoded Default Paths as absolute fallback
            string[] fallbackRoots = new[]
            {
                $@"C:\ProgramData\Autodesk\RVT {appVersion}\Family Templates\English",
                $@"C:\ProgramData\Autodesk\RVT {appVersion}\Family Templates\English-Imperial"
            };

            foreach (var fr in fallbackRoots)
            {
                if (!Directory.Exists(fr)) continue;
                foreach (var name in possibleFiles)
                {
                    string fullPath = Path.Combine(fr, name);
                    if (File.Exists(fullPath)) return fullPath;
                }
            }

            return string.Empty;
        }

        private static bool IsExpectedTemplateSearchException(Exception ex)
        {
            return ex is IOException
                || ex is UnauthorizedAccessException
                || ex is ArgumentException;
        }
    }
}
