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
            string appVersion = app.VersionNumber;
            string templateName = "";

            if (category.Id.Value == (long)BuiltInCategory.OST_Doors)
                templateName = "LECG_047_DOORS.rft";
            else if (category.Id.Value == (long)BuiltInCategory.OST_Windows)
                templateName = "LECG_179_WINDOWS.rft";
            else if (category.Id.Value == (long)BuiltInCategory.OST_Furniture)
                templateName = "Metric Generic Model.rft";

            if (!string.IsNullOrEmpty(templateName) && !templateName.Contains("Generic"))
            {
                string customPath = $@"C:\ProgramData\Autodesk\RVT {appVersion}\Family Templates\English\LECG\-\{templateName}";
                if (File.Exists(customPath)) return customPath;

                string specificPath2026 = $@"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\LECG\-\{templateName}";
                if (File.Exists(specificPath2026)) return specificPath2026;
            }

            string rootPath = app.FamilyTemplatePath;
            string[] possibleNames = new[] { "Metric Generic Model.rft", "Generic Model.rft" };

            foreach (var name in possibleNames)
            {
                string fullPath = Path.Combine(rootPath, name);
                if (File.Exists(fullPath)) return fullPath;
            }

            try
            {
                if (Directory.Exists(rootPath))
                {
                    var files = Directory.GetFiles(rootPath, "*Generic Model.rft", SearchOption.AllDirectories);
                    var match = files.FirstOrDefault(f => Path.GetFileName(f).Equals("Metric Generic Model.rft", StringComparison.OrdinalIgnoreCase))
                                ?? files.FirstOrDefault(f => Path.GetFileName(f).Equals("Generic Model.rft", StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match;
                }
            }
            catch
            {
            }

            return string.Empty;
        }
    }
}
