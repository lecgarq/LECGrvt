using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class CadImportFamilyCreationService : ICadImportFamilyCreationService
    {
        private readonly ICadFamilyBuildService _cadFamilyBuildService;
        private readonly ICadFamilyLoadPlacementService _familyLoadPlacementService;

        public CadImportFamilyCreationService(ICadFamilyBuildService cadFamilyBuildService, ICadFamilyLoadPlacementService familyLoadPlacementService)
        {
            _cadFamilyBuildService = cadFamilyBuildService;
            _familyLoadPlacementService = familyLoadPlacementService;
        }

        public ElementId CreateAndLoad(Document doc, CadData data, XYZ center, ElementId originalCadElementId, string familyName, string templatePath, string lineStyleName, Color lineColor, int lineWeight, Action<double, string>? progress = null)
        {
            progress?.Invoke(50, "Creating family document...");
            string path = _cadFamilyBuildService.BuildAndSave(
                doc,
                templatePath,
                data,
                center,
                lineStyleName,
                lineColor,
                lineWeight,
                "Create Detail Item Content",
                familyName,
                progress,
                50,
                80);

            progress?.Invoke(90, "Saving and loading family...");
            return _familyLoadPlacementService.LoadAndPlace(doc, path, center, originalCadElementId);
        }
    }
}
