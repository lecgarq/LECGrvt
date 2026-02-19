using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Services
{
    public class FamilyConversionNamingService : IFamilyConversionNamingService
    {
        public string ResolveTargetFamilyName(Document doc, string sourceFamilyName, string customName)
        {
            var existingNames = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Select(f => f.Name)
                .ToList();

            return LECG.Core.Naming.FamilyNamePolicy.ResolveName(existingNames, sourceFamilyName, customName);
        }
    }
}
