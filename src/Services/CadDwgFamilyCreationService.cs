using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadDwgFamilyCreationService : ICadDwgFamilyCreationService
    {
        private readonly ICadFamilyBuildService _cadFamilyBuildService;
        private readonly ICadFamilyLoadPlacementService _familyLoadPlacementService;

        public CadDwgFamilyCreationService(ICadFamilyBuildService cadFamilyBuildService, ICadFamilyLoadPlacementService familyLoadPlacementService)
        {
            _cadFamilyBuildService = cadFamilyBuildService;
            _familyLoadPlacementService = familyLoadPlacementService;
        }

        public ElementId CreateAndLoad(Document doc, CadData data, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            progress?.Invoke(50, "Creating final family...");
            string path = _cadFamilyBuildService.BuildAndSave(
                doc,
                templatePath,
                data,
                XYZ.Zero,
                lineStyleName,
                lineColor,
                lineWeight,
                "Create Detail Item",
                familyName,
                progress,
                50,
                90);

            progress?.Invoke(95, "Loading into project...");
            return _familyLoadPlacementService.LoadOnly(doc, path);
        }
    }
}
