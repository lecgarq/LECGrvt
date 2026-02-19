using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Linq;

namespace LECG.Services
{
    public class FamilyConversionNamingService : IFamilyConversionNamingService
    {
        public string ResolveTargetFamilyName(Document doc, string sourceFamilyName, string customName)
        {
            string baseName = string.IsNullOrWhiteSpace(customName) ? $"{sourceFamilyName}_Converted" : customName;
            string finalName = baseName;
            int counter = 1;

            var existingFamilies = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Select(f => f.Name)
                .ToList();

            while (existingFamilies.Contains(finalName))
            {
                finalName = $"{baseName}_{counter}";
                counter++;
            }

            return finalName;
        }
    }
}
